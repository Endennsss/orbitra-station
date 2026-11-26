using Content.Shared._Orbitra.Surgery;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Surgery;

/// <summary>Проверяет показания и инструменты, привязывает текущий шаг к пациенту и хирургу.</summary>
public sealed partial class OrbitraSurgerySystem : EntitySystem
{
    [Dependency] private OrbitraSurgeryAnatomySystem _anatomy = default!;
    [Dependency] private OrbitraTraumaSystem _trauma = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly SurgeryZone[] Zones = Enum.GetValues<SurgeryZone>();
    private static readonly EntProtoId ExtractedFragment = "OrbitraExtractedFragment";
    private const float Reach = 1.5f;
    private const float StepSeconds = 3f;
    private int _nextTicket;

    public override void Initialize()
    {
        base.Initialize();
        InitializeLimbs();
        InitializeMonitor();
        Subs.BuiEvents<OrbitraSurgeryTableComponent>(OrbitraSurgeryUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>((Entity<OrbitraSurgeryTableComponent> ent, ref BoundUIOpenedEvent _) => Publish(ent));
            subs.Event<BoundUIClosedEvent>(OnClosed);
            subs.Event<OrbitraSurgeryStepMessage>(OnStep);
            subs.Event<OrbitraSurgeryCancelMessage>(OnCancel);
        });
        SubscribeLocalEvent<OrbitraSurgeryTableComponent, StrapAttemptEvent>(OnStrapAttempt);
        SubscribeLocalEvent<OrbitraSurgeryTableComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<OrbitraSurgeryTableComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<OrbitraSurgeryOperationComponent, OrbitraSurgeryDoAfterEvent>(OnStepFinished);
        SubscribeLocalEvent<DoAfterComponent, ComponentShutdown>(OnSurgeonShutdown);
        _trauma.Changed += OnTraumaChanged;
    }

    public override void Shutdown()
    {
        _trauma.Changed -= OnTraumaChanged;
        base.Shutdown();
    }

    private EntityUid? GetPatient(EntityUid table)
    {
        if (!TryComp<StrapComponent>(table, out var strap) || strap.BuckledEntities.Count != 1)
            return null;

        foreach (var patient in strap.BuckledEntities)
        {
            if (!TerminatingOrDeleted(patient) && TryComp<BuckleComponent>(patient, out var buckle) && buckle.BuckledTo == table)
                return patient;
        }

        return null;
    }

    private bool CanOperate(EntityUid table, EntityUid user, EntityUid patient)
    {
        return user != patient && !TerminatingOrDeleted(table) && !TerminatingOrDeleted(user) &&
               HasComp<OrbitraSurgeryTableComponent>(table) && GetPatient(table) == patient &&
               _ui.IsUiOpen(table, OrbitraSurgeryUiKey.Key, user) &&
               _blocker.CanComplexInteract(user) && _blocker.CanInteract(user, patient) &&
               _interaction.IsAccessible(user, table) && _interaction.InRangeUnobstructed(user, table, Reach);
    }

    private static SurgeryInjury Indication(SurgeryProcedure procedure) => procedure switch
    {
        SurgeryProcedure.Fracture => SurgeryInjury.Fracture,
        SurgeryProcedure.InternalBleeding => SurgeryInjury.InternalBleeding,
        SurgeryProcedure.Shrapnel => SurgeryInjury.Shrapnel,
        _ => SurgeryInjury.None,
    };

    private bool HasIndication(EntityUid patient, SurgeryZone zone, SurgeryProcedure procedure, SurgeryOrgan? organ)
    {
        if (!_anatomy.SupportsZone(patient, zone))
            return false;

        if (OrbitraOrganSystem.IsProcedure(procedure))
            return zone == SurgeryZone.Torso && organ is { } kind && CanOperateOnOrgan(patient, kind, procedure);
        if (organ != null)
            return false;

        if (procedure == SurgeryProcedure.Amputate)
            return TryGetLimb(patient, zone, out _, out var root, out _) && !HasComp<OrbitraProstheticJointComponent>(root);
        if (procedure == SurgeryProcedure.InstallProsthesis)
            return CanFitProsthesis(patient, zone, out _, out _);
        if (procedure == SurgeryProcedure.RemoveProsthesis)
            return CanRemoveProsthesis(patient, zone, out _, out _);

        var injuries = _trauma.GetInjuries(patient, zone);
        if ((injuries & SurgeryInjury.Missing) != 0)
            return procedure == SurgeryProcedure.Reattach && OrbitraSurgeryAnatomySystem.Extremity(zone) != null;

        return (injuries & Indication(procedure)) != 0;
    }

    private bool HasTool(EntityUid patient, SurgeryZone zone, EntityUid? tool, string expected, SurgeryOrgan? organ)
    {
        if (tool == null || TerminatingOrDeleted(tool.Value))
            return false;

        if (expected == OrbitraSurgerySteps.Prosthesis)
            return CanUseProsthesis(tool.Value, zone, out _);

        if (expected == OrbitraSurgerySteps.DonorOrgan)
            return organ is { } kind && _organs.IsDonor(tool.Value, kind);

        if (expected == "DetachedBody")
            return CanReattachLimb(patient, zone, tool.Value, out _, out _, out _);

        return TryComp<OrbitraSurgicalToolComponent>(tool, out var surgical) && surgical.Kind == expected;
    }

    private void OnStep(Entity<OrbitraSurgeryTableComponent> table, ref OrbitraSurgeryStepMessage args)
    {
        if (TryGetEntity(args.Patient, out var patient))
            TryStartStep(table, args.Actor, patient.Value, args.Zone, args.Procedure, args.Step, args.Confirmed, args.Organ);
    }

    /// <summary>Начинает ожидаемый сервером шаг в открытом интерфейсе стола с этим пациентом.</summary>
    public bool TryStartStep(EntityUid table, EntityUid user, EntityUid patient, SurgeryZone zone,
        SurgeryProcedure procedure, int step, bool confirmed, SurgeryOrgan? organ = null)
    {
        var steps = OrbitraSurgerySteps.Get(procedure);
        if (!TryComp<OrbitraSurgeryTableComponent>(table, out var station) ||
            !OrbitraSurgeryTableComponent.Supports(station.FieldTable, procedure) ||
            (uint) step >= (uint) steps.Length || !CanOperate(table, user, patient) ||
            !HasComp<DoAfterComponent>(user) || !HasIndication(patient, zone, procedure, organ) ||
            (procedure is SurgeryProcedure.Amputate or SurgeryProcedure.ExtractOrgan && step == 0 && !confirmed))
            return false;

        if (TryComp<OrbitraSurgeryOperationComponent>(patient, out var operation))
        {
            if (operation.Surgeon != null || operation.Zone != zone || operation.Procedure != procedure ||
                operation.Organ != organ || !MatchesOriginalOrgan(patient, operation) || operation.Step != step)
                return false;
        }
        else if (step != 0)
            return false;

        var tool = _hands.GetActiveItem(user);
        if (!HasTool(patient, zone, tool, steps[step], organ))
        {
            var key = steps[step] == "DetachedBody" ? "orbitra-surgery-wrong-limb" : "orbitra-surgery-wrong-tool";
            _popup.PopupEntity(Loc.GetString(key), table, user);
            return false;
        }

        operation ??= EnsureComp<OrbitraSurgeryOperationComponent>(patient);
        operation.Zone = zone;
        operation.Procedure = procedure;
        operation.Organ = organ;
        operation.OriginalOrgan = organ is { } kind ? _organs.Installed(patient, kind) : null;
        operation.Table = table;
        operation.Surgeon = user;
        operation.Ticket = ++_nextTicket;
        var speed = TryComp<ToolComponent>(tool, out var toolComp) ? Math.Clamp(toolComp.SpeedModifier, 0.25f, 4f) : 1f;
        var ev = new OrbitraSurgeryDoAfterEvent { Ticket = operation.Ticket };
        var args = new DoAfterArgs(EntityManager, user, StepSeconds * station.StepDurationMultiplier / speed, ev, patient, patient, tool)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = Reach,
            DuplicateCondition = DuplicateConditions.SameEvent,
            CancelDuplicate = false,
        };
        operation.StartedAt = _timing.CurTime;
        if (!_doAfter.TryStartDoAfter(args, out var id))
        {
            Release(patient, operation);
            Publish(table);
            return false;
        }

        // Мгновенный DoAfter успевает завершиться и снять блокировку ещё внутри TryStartDoAfter.
        if (operation.Surgeon == user && operation.Ticket == ev.Ticket)
        {
            operation.Pending = id;
            operation.Duration = args.Delay;
        }
        Publish(table);
        return true;
    }

    private void OnStepFinished(Entity<OrbitraSurgeryOperationComponent> patient, ref OrbitraSurgeryDoAfterEvent args)
    {
        var op = patient.Comp;
        if (args.Handled || op.Ticket != args.Ticket || op.Surgeon != args.User || op.Table is not { } table)
            return;

        args.Handled = true;
        var steps = OrbitraSurgerySteps.Get(op.Procedure);
        if (!args.Cancelled && args.Used is { } tool && (uint) op.Step < (uint) steps.Length &&
            CanOperate(table, args.User, patient) && HasIndication(patient, op.Zone, op.Procedure, op.Organ) && MatchesOriginalOrgan(patient, op) &&
            _hands.GetActiveItem(args.User) == tool && HasTool(patient, op.Zone, tool, steps[op.Step], op.Organ))
        {
            if (op.Step + 1 < steps.Length)
                op.Step++;
            else
            {
                // Устранение показания синхронно поднимает Changed внутри завершающего шага.
                op.Finishing = true;
                try
                {
                    if (TryFinishProcedure(patient, op.Zone, op.Procedure, tool, op.Organ))
                        op.Step = 0;
                }
                finally
                {
                    op.Finishing = false;
                }
            }
        }

        Release(patient, op);
        Publish(table);
    }

    private bool TryFinishProcedure(EntityUid patient, SurgeryZone zone, SurgeryProcedure procedure, EntityUid tool, SurgeryOrgan? organ)
    {
        if (organ is { } kind)
            return TryFinishOrganProcedure(patient, kind, procedure, tool);

        if (procedure == SurgeryProcedure.Amputate)
            return TryDetachLimb(patient, zone, out _);
        if (procedure == SurgeryProcedure.InstallProsthesis)
            return TryInstallProsthesis(patient, zone, tool);
        if (procedure == SurgeryProcedure.RemoveProsthesis)
            return TryRemoveProsthesis(patient, zone, out _);
        if (procedure == SurgeryProcedure.Reattach)
            return TryReattachLimb(patient, zone, tool);
        EntProtoId? source = null;
        if (procedure == SurgeryProcedure.Shrapnel && TryComp<OrbitraTraumaComponent>(patient, out var trauma) &&
            trauma.FragmentSources.TryGetValue(zone, out var projectile))
            source = projectile;
        if (!_trauma.TryTreat(patient, zone, Indication(procedure)))
            return false;

        if (procedure == SurgeryProcedure.Shrapnel)
        {
            var fragment = SpawnNextToOrDrop(ExtractedFragment, patient);
            var evidence = Comp<OrbitraFragmentEvidenceComponent>(fragment);
            evidence.Projectile = source;
            Dirty(fragment, evidence);
        }
        return true;
    }

    private void Release(EntityUid patient, OrbitraSurgeryOperationComponent operation)
    {
        operation.Surgeon = null;
        operation.Table = null;
        operation.Pending = null;
        if (operation.Step == 0)
            RemComp<OrbitraSurgeryOperationComponent>(patient);
    }

    private void OnCancel(Entity<OrbitraSurgeryTableComponent> table, ref OrbitraSurgeryCancelMessage args)
    {
        if (TryGetEntity(args.Patient, out var patient))
            TryAbandon(table, args.Actor, patient.Value, args.Zone, args.Procedure, args.Step, args.Organ);
    }

    /// <summary>Отменяет подготовку между шагами. Завершение активного DoAfter проверяется отдельно.</summary>
    public bool TryAbandon(EntityUid table, EntityUid user, EntityUid patient, SurgeryZone zone,
        SurgeryProcedure procedure, int step, SurgeryOrgan? organ = null)
    {
        if (!CanOperate(table, user, patient) || !TryComp<OrbitraSurgeryOperationComponent>(patient, out var op) ||
            op.Surgeon != null || op.Zone != zone || op.Procedure != procedure || op.Organ != organ || op.Step != step)
            return false;

        RemComp<OrbitraSurgeryOperationComponent>(patient);
        Publish(table);
        return true;
    }

    private void OnClosed(Entity<OrbitraSurgeryTableComponent> table, ref BoundUIClosedEvent args)
    {
        CloseMonitor(table, args.Actor);
        if (GetPatient(table) is { } patient && TryComp<OrbitraSurgeryOperationComponent>(patient, out var op) &&
            op.Table == table.Owner && op.Surgeon == args.Actor)
            Interrupt(patient, op);
    }

    private void OnSurgeonShutdown(Entity<DoAfterComponent> user, ref ComponentShutdown args)
    {
        foreach (var action in user.Comp.DoAfters.Values)
        {
            if (action.Args.Event is not OrbitraSurgeryDoAfterEvent ev || action.Args.Target is not { } patient ||
                !TryComp<OrbitraSurgeryOperationComponent>(patient, out var op) || op.Ticket != ev.Ticket ||
                op.Surgeon != user.Owner || op.Table is not { } table)
                continue;

            Release(patient, op);
            Publish(table);
        }
    }

    private void OnStrapAttempt(Entity<OrbitraSurgeryTableComponent> table, ref StrapAttemptEvent args)
    {
        if (args.Strap.Comp.BuckledEntities.Count != 0)
            args.Cancelled = true;
    }

    private void OnStrapped(Entity<OrbitraSurgeryTableComponent> table, ref StrappedEvent args)
    {
        Publish(table);
    }

    private void OnUnstrapped(Entity<OrbitraSurgeryTableComponent> table, ref UnstrappedEvent args)
    {
        if (TryComp<OrbitraSurgeryOperationComponent>(args.Buckle, out var op) && op.Table == table.Owner)
            Interrupt(args.Buckle, op);
        Publish(table);
    }

    private void Interrupt(EntityUid patient, OrbitraSurgeryOperationComponent op)
    {
        if (_doAfter.IsRunning(op.Pending))
            _doAfter.Cancel(op.Pending);
        else
            Release(patient, op);
    }

    private void OnTraumaChanged(EntityUid patient)
    {
        if (TryComp<OrbitraSurgeryOperationComponent>(patient, out var op))
        {
            if (op.Finishing)
                return;

            if (!HasIndication(patient, op.Zone, op.Procedure, op.Organ) || !MatchesOriginalOrgan(patient, op))
            {
                var pending = op.Pending;
                RemComp<OrbitraSurgeryOperationComponent>(patient);
                if (_doAfter.IsRunning(pending))
                    _doAfter.Cancel(pending);
            }
        }

        if (TryComp<BuckleComponent>(patient, out var buckle) && buckle.BuckledTo is { } table &&
            HasComp<OrbitraSurgeryTableComponent>(table))
            Publish(table);
    }

    private void Publish(EntityUid table)
    {
        if (TerminatingOrDeleted(table) || !_ui.IsUiOpen(table, OrbitraSurgeryUiKey.Key))
            return;

        var patient = GetPatient(table);
        UpdateMonitorPatients(table, patient);
        var injuries = new Dictionary<SurgeryZone, SurgeryInjury>();
        OrbitraSurgeryOperationComponent? op = null;
        if (patient is { } occupant)
        {
            foreach (var zone in Zones)
            {
                if (_anatomy.SupportsZone(occupant, zone))
                    injuries.Add(zone, _trauma.GetInjuries(occupant, zone));
            }

            TryComp(occupant, out op);
        }

        var busy = op?.Surgeon != null;
        _ui.SetUiState(table, OrbitraSurgeryUiKey.Key, new OrbitraSurgeryState(GetNetEntity(patient),
            patient is { } uid ? Name(uid) : string.Empty, injuries, op?.Zone, op?.Procedure, op?.Step ?? 0,
            busy, busy ? op!.StartedAt : null, busy ? op!.Duration : default,
            patient is { } body ? _organs.GetConditions(body) : null, op?.Organ)
        {
            AcceptsProstheses = HasComp<OrbitraHumanOrganPatientComponent>(patient),
            Prostheses = TryComp<OrbitraFittedProsthesesComponent>(patient, out var fitted) ? new(fitted.Parts) : new(),
            FieldTable = Comp<OrbitraSurgeryTableComponent>(table).FieldTable,
        });
    }
}
