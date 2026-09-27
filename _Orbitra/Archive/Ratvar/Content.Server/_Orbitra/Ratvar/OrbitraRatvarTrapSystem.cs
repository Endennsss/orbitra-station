using Content.Shared._Orbitra.Ratvar;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Robust.Server.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Server-authoritative, bounded trap signals isolated by cult and grid.</summary>
public sealed partial class OrbitraRatvarTrapSystem : EntitySystem
{
    [Dependency] private OrbitraRatvarRuleSystem _cult = default!;
    [Dependency] private OrbitraRatvarPowerSystem _power = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    private TimeSpan _nextUpdate;
    private readonly List<(Entity<OrbitraRatvarTrapComponent> Trap, OrbitraRatvarTrapPulse Pulse)> _readySignals = new();
    private readonly Queue<(Entity<OrbitraRatvarTrapComponent> Trap, EntityUid Target)> _steps = new();

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(StepTriggerSystem));
        SubscribeLocalEvent<OrbitraRatvarTrapComponent, InteractUsingEvent>(OnWire);
        SubscribeLocalEvent<OrbitraRatvarTrapComponent, InteractHandEvent>(OnHand);
        SubscribeLocalEvent<OrbitraRatvarTrapComponent, StepTriggerAttemptEvent>(OnStepAttempt);
        SubscribeLocalEvent<OrbitraRatvarTrapComponent, StepTriggeredOnEvent>(OnStep);
        SubscribeLocalEvent<OrbitraRatvarTrapComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<OrbitraRatvarTrapComponent, AnchorStateChangedEvent>(OnAnchor);
        InitializeEffects();
    }

    private void OnExamine(Entity<OrbitraRatvarTrapComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange && _cult.TryGetCult(args.Examiner, out var rule) &&
            TryComp<OrbitraRatvarStructureComponent>(ent, out var structure) && structure.Rule == rule.Owner)
            args.PushMarkup(Loc.GetString("orbitra-ratvar-trap-wiring", ("count", ent.Comp.Outputs.Count)));
    }

    public override void Shutdown()
    {
        _steps.Clear();
        _readySignals.Clear();
        base.Shutdown();
    }

    private void OnWire(Entity<OrbitraRatvarTrapComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<OrbitraRatvarTabletComponent>(args.Used) ||
            HasComp<ActiveOrbitraRatvarEmpowermentComponent>(args.Used)) return;
        args.Handled = TrySelectLink(ent, args.User, args.Used);
    }

    /// <summary>Selects a sender or toggles a validated outgoing link using a held tablet.</summary>
    public bool TrySelectLink(Entity<OrbitraRatvarTrapComponent> target, EntityUid user, EntityUid tablet)
    {
        if (!CanConfigure(target, user, tablet, out var owner)) return false;
        var message = "orbitra-ratvar-trap-link-invalid";
        if (TryComp<OrbitraRatvarTrapLinkComponent>(tablet, out var selection) &&
            selection.User == user && selection.Rule == owner && !TerminatingOrDeleted(selection.Source))
        {
            if (selection.Source == target.Owner)
                message = "orbitra-ratvar-trap-link-cancelled";
            else if (TryLink(selection.Source, target, user, tablet))
                message = "orbitra-ratvar-trap-linked";
            RemComp<OrbitraRatvarTrapLinkComponent>(tablet);
        }
        else if (target.Comp.Sender)
        {
            var link = EnsureComp<OrbitraRatvarTrapLinkComponent>(tablet);
            link.Source = target;
            link.User = user;
            link.Rule = owner;
            message = "orbitra-ratvar-trap-selected";
        }
        _popup.PopupEntity(Loc.GetString(message), target, user);
        return true;
    }

    private bool CanConfigure(EntityUid target, EntityUid user, EntityUid tablet, out EntityUid owner)
    {
        owner = default;
        if (!_cult.TryGetCult(user, out var rule) || !_mob.IsAlive(user) ||
            !_hands.IsHolding(user, tablet) || !TryComp<OrbitraRatvarTabletComponent>(tablet, out var slab) ||
            slab.Busy || HasComp<ActiveOrbitraRatvarEmpowermentComponent>(tablet) ||
            !_blocker.CanInteract(user, target) || !_interaction.InRangeUnobstructed(user, target) ||
            !TryComp<OrbitraRatvarStructureComponent>(target, out var structure) || structure.Rule != rule.Owner)
            return false;
        owner = rule;
        return true;
    }

    /// <summary>Toggles one edge, rejecting foreign, distant or cyclic wiring.</summary>
    public bool TryLink(EntityUid source, EntityUid target, EntityUid user, EntityUid tablet)
    {
        if (!CanLink(source, target, user, tablet)) return false;
        var outputs = Comp<OrbitraRatvarTrapComponent>(source).Outputs;
        if (!outputs.Remove(target)) outputs.Add(target);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"Ratvar: {ToPrettyString(user)} changed trap link {ToPrettyString(source)} -> {ToPrettyString(target)}.");
        return true;
    }

    /// <summary>Pure wiring validation. Removing an existing link remains possible at the output limit.</summary>
    public bool CanLink(EntityUid source, EntityUid target, EntityUid user, EntityUid tablet)
    {
        if (source == target || !CanConfigure(target, user, tablet, out var owner) ||
            !TryComp<OrbitraRatvarTrapComponent>(source, out var sender) || !sender.Sender ||
            !TryComp<OrbitraRatvarTrapComponent>(target, out var receiver) || !receiver.Receiver ||
            !TryComp<OrbitraRatvarStructureComponent>(source, out var structure) || structure.Rule != owner ||
            !Transform(source).Anchored || !Transform(target).Anchored ||
            Transform(source).GridUid is not { } grid || Transform(target).GridUid != grid ||
            !_transform.InRange(source, target, sender.LinkRange) ||
            _container.IsEntityInContainer(source) || _container.IsEntityInContainer(target)) return false;
        if (sender.Outputs.Contains(target)) return true;
        if (sender.Outputs.Count >= sender.MaxOutputs) return false;
        var visited = new HashSet<EntityUid>();
        var pending = new Stack<EntityUid>();
        pending.Push(target);
        while (pending.TryPop(out var next))
        {
            if (next == source) return false;
            if (!visited.Add(next)) continue;
            if (visited.Count > 64) return false;
            if (!TryComp<OrbitraRatvarTrapComponent>(next, out var trap)) continue;
            foreach (var output in trap.Outputs) pending.Push(output);
        }
        return true;
    }

    private void OnHand(Entity<OrbitraRatvarTrapComponent> ent, ref InteractHandEvent args)
    {
        if (!args.Handled && ent.Comp.Kind == OrbitraRatvarTrapKind.Lever)
            args.Handled = TryPullLever(ent, args.User);
    }

    /// <summary>Anyone able to interact may pull a lever, matching Bee's counterplay.</summary>
    public bool TryPullLever(Entity<OrbitraRatvarTrapComponent> ent, EntityUid user)
    {
        if (!CanPullLever(ent, user)) return false;
        return TryActivate(ent);
    }

    private bool CanPullLever(Entity<OrbitraRatvarTrapComponent> ent, EntityUid user) =>
        ent.Comp.Kind == OrbitraRatvarTrapKind.Lever && _mob.IsAlive(user) &&
        _blocker.CanInteract(user, ent) && _interaction.InRangeUnobstructed(user, ent.Owner);

    private void OnStepAttempt(Entity<OrbitraRatvarTrapComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = ent.Comp.Kind == OrbitraRatvarTrapKind.Plate && CanStep(ent, args.Tripper);
    }

    private void OnStep(Entity<OrbitraRatvarTrapComponent> ent, ref StepTriggeredOnEvent args)
    {
        // Удержание меняет контакты; не изменяем коллекцию StepTrigger из его собственного события.
        if (_steps.Count < 256 && CanStep(ent, args.Tripper)) _steps.Enqueue((ent, args.Tripper));
    }

    /// <summary>Pressure plates ignore their own cult and non-living or airborne occupants.</summary>
    public bool CanStep(Entity<OrbitraRatvarTrapComponent> ent, EntityUid target) =>
        ent.Comp.Kind == OrbitraRatvarTrapKind.Plate && GroundedLiving(target) &&
        (!TryComp<Content.Shared.Buckle.Components.BuckleComponent>(target, out var buckle) || !buckle.Buckled) &&
        TryComp<OrbitraRatvarStructureComponent>(ent, out var structure) && structure.Rule != null &&
        (!_cult.TryGetCult(target, out var rule) || rule.Owner != structure.Rule);

    private bool CanOperate(EntityUid uid, EntityUid? owner = null, EntityUid? grid = null) =>
        TryComp<OrbitraRatvarPoweredComponent>(uid, out var powered) &&
        _power.CanUsePower((uid, powered), out var rule, out _) &&
        (owner == null || owner == rule.Owner) && (grid == null || Transform(uid).GridUid == grid);

    /// <summary>Starts a fresh bounded pulse; internal receivers cannot be triggered as public senders.</summary>
    public bool TryActivate(Entity<OrbitraRatvarTrapComponent> ent)
    {
        if (!CanActivate(ent)) return false;
        var pulse = new OrbitraRatvarTrapPulse(Comp<OrbitraRatvarStructureComponent>(ent).Rule!.Value,
            Transform(ent).GridUid!.Value);
        pulse.Visited.Add(ent);
        ent.Comp.NextUse = _timing.CurTime + ent.Comp.Cooldown;
        _audio.PlayPvs(ent.Comp.Sound, ent);
        Send(ent, pulse);
        return true;
    }

    private bool CanActivate(Entity<OrbitraRatvarTrapComponent> ent) =>
        ent.Comp.Sender && ent.Comp.Kind != OrbitraRatvarTrapKind.Delay &&
        ent.Comp.NextUse <= _timing.CurTime && CanOperate(ent);

    private void Send(Entity<OrbitraRatvarTrapComponent> ent, OrbitraRatvarTrapPulse pulse)
    {
        // Удалённые выходы не занимают лимит постоянно; обход ограничен восемью связями.
        ent.Comp.Outputs.RemoveWhere(uid => TerminatingOrDeleted(uid));
        foreach (var output in ent.Comp.Outputs)
            if (_transform.InRange(ent.Owner, output, ent.Comp.LinkRange)) Receive(output, pulse);
        if (ent.Comp.Kind != OrbitraRatvarTrapKind.Plate) return;
        var local = _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 0.8f);
        foreach (var other in local)
            if (_transform.TryGetGridTilePosition(ent.Owner, out var tile) &&
                _transform.TryGetGridTilePosition(other, out var otherTile) && tile == otherTile)
                Receive(other, pulse);
    }

    private void Receive(EntityUid uid, OrbitraRatvarTrapPulse pulse)
    {
        if (!TryComp<OrbitraRatvarTrapComponent>(uid, out var trap) || !trap.Receiver ||
            pulse.Visited.Count >= 64 || !pulse.Visited.Add(uid) ||
            !CanOperate(uid, pulse.Rule, pulse.Grid)) return;
        if (trap.Kind == OrbitraRatvarTrapKind.Skewer && trap.Extended)
        {
            Retract((uid, trap));
            return;
        }
        if (trap.NextUse > _timing.CurTime || HasComp<ActiveOrbitraRatvarTrapComponent>(uid)) return;
        if (!_power.TryUsePower((uid, Comp<OrbitraRatvarPoweredComponent>(uid)))) return;
        trap.NextUse = _timing.CurTime + trap.Cooldown;
        _audio.PlayPvs(trap.Sound, uid);
        var active = EnsureComp<ActiveOrbitraRatvarTrapComponent>(uid);
        active.Rule = pulse.Rule;
        active.Grid = pulse.Grid;
        active.FinishAt = _timing.CurTime + trap.Delay;
        active.Pulse = trap.Kind == OrbitraRatvarTrapKind.Delay ? pulse : null;
        _appearance.SetData(uid, OrbitraRatvarVisuals.Trap, true);
        if (trap.Kind == OrbitraRatvarTrapKind.Skewer) Impale((uid, trap));
        if (trap.Kind == OrbitraRatvarTrapKind.Flipper) Flip((uid, trap));
    }

    private void OnAnchor(Entity<OrbitraRatvarTrapComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored) return;
        ent.Comp.Outputs.Clear();
        Retract(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        while (_steps.TryDequeue(out var step))
            if (!TerminatingOrDeleted(step.Trap) && !TerminatingOrDeleted(step.Target) &&
                SameTile(step.Trap, step.Target) && CanStep(step.Trap, step.Target)) TryActivate(step.Trap);
        if (_nextUpdate > _timing.CurTime) return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.1);
        _readySignals.Clear();
        var query = EntityQueryEnumerator<ActiveOrbitraRatvarTrapComponent, OrbitraRatvarTrapComponent>();
        while (query.MoveNext(out var uid, out var active, out var trap))
        {
            if (!CanOperate(uid, active.Rule, active.Grid))
            {
                Retract((uid, trap));
                continue;
            }
            if (trap.Extended || active.FinishAt > _timing.CurTime) continue;
            var pulse = active.Pulse;
            active.Pulse = null;
            RemCompDeferred<ActiveOrbitraRatvarTrapComponent>(uid);
            _appearance.SetData(uid, OrbitraRatvarVisuals.Trap, false);
            if (pulse != null) _readySignals.Add(((uid, trap), pulse));
        }
        // Передача может добавить Active-компонент приёмнику: не меняем выборку во время обхода.
        foreach (var (trap, pulse) in _readySignals)
            if (CanOperate(trap, pulse.Rule, pulse.Grid)) Send(trap, pulse);
        _readySignals.Clear();
    }
}
