using Content.Server.GameTicking;
using Content.Server.Construction;
using Content.Shared.ActionBlocker;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Server.Mind;
using Content.Server.Power.Components;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Wires;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Installs cogs in APCs and debits real battery charge before crediting a single cult.</summary>
public sealed partial class OrbitraRatvarIntegrationCogSystem : EntitySystem
{
    [Dependency] private OrbitraRatvarRuleSystem _cult = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedWiresSystem _wires = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MindSystem _minds = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    private const string ContainerId = "orbitra-ratvar-cog";
    private static readonly ProtoId<ToolQualityPrototype> PryQuality = "Prying";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarIntegrationCogComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<OrbitraRatvarIntegrationCogComponent, OrbitraRatvarCogEvent>(OnInstallFinished);
        SubscribeLocalEvent<OrbitraRatvarIntegrationCogComponent, DoAfterAttemptEvent<OrbitraRatvarCogEvent>>(OnInstallAttempt);
        SubscribeLocalEvent<OrbitraRatvarInstalledCogComponent, InteractUsingEvent>(OnRemove, before: [typeof(ConstructionSystem)]);
        SubscribeLocalEvent<OrbitraRatvarInstalledCogComponent, OrbitraRatvarCogRemoveEvent>(OnRemoveFinished);
        SubscribeLocalEvent<OrbitraRatvarInstalledCogComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<OrbitraRatvarCogOperationsComponent, ComponentShutdown>(OnOperationsShutdown);
    }

    private void OnInteract(Entity<OrbitraRatvarIntegrationCogComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<ApcComponent>(target))
            return;
        args.Handled = true;
        if (!TryStartInstall(ent, args.User, target))
            _popup.PopupEntity(Loc.GetString("orbitra-ratvar-cog-install-denied"), target, args.User);
    }

    /// <summary>Starts opening or installing; both completions repeat all authority checks.</summary>
    public bool TryStartInstall(Entity<OrbitraRatvarIntegrationCogComponent> cog, EntityUid user, EntityUid apc)
    {
        if (!CanInstall(cog, user, apc, out var rule) || !_minds.TryGetMind(user, out var mind, out _))
            return false;
        var open = !Comp<WiresPanelComponent>(apc).Open;
        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user,
            open ? cog.Comp.OpenDelay : cog.Comp.InstallDelay,
            new OrbitraRatvarCogEvent { OpenPanel = open, Rule = GetNetEntity(rule), Mind = GetNetEntity(mind) },
            cog, target: apc, used: cog)
        {
            BreakOnMove = true, BreakOnDamage = true, BreakOnHandChange = true, NeedHand = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        }, out var id);
        TrackOperation(apc, id);
        return started;
    }

    private void OnInstallAttempt(Entity<OrbitraRatvarIntegrationCogComponent> ent, ref DoAfterAttemptEvent<OrbitraRatvarCogEvent> args)
    {
        var operation = args.DoAfter.Args;
        var context = (OrbitraRatvarCogEvent) operation.Event;
        if (operation.Target is not { } apc || !CanInstall(ent, operation.User, apc, out var rule) ||
            rule != GetEntity(context.Rule) || !_minds.TryGetMind(operation.User, out var mind, out _) ||
            mind != GetEntity(context.Mind) || (!context.OpenPanel && !Comp<WiresPanelComponent>(apc).Open))
            args.Cancel();
    }

    private bool CanInstall(EntityUid cog, EntityUid user, EntityUid apc, out EntityUid rule)
    {
        rule = default;
        if (!_cult.TryGetCult(user, out var cult) || !_hands.IsHolding(user, cog) || !_blocker.CanInteract(user, cog) ||
            !HasComp<ApcComponent>(apc) || !HasComp<BatteryComponent>(apc) ||
            !HasComp<WiresPanelComponent>(apc) || !Transform(apc).Anchored ||
            !_interaction.InRangeUnobstructed(user, apc) || GetCog(apc) != null)
            return false;
        rule = cult.Owner;
        return true;
    }

    private void OnInstallFinished(Entity<OrbitraRatvarIntegrationCogComponent> ent, ref OrbitraRatvarCogEvent args)
    {
        ForgetOperation(args.Target, args.DoAfter.Id);
        if (args.Handled || args.Cancelled) return;
        args.Handled = true;
        if (args.Target is not { } apc || !CanInstall(ent, args.User, apc, out var rule) ||
            rule != GetEntity(args.Rule) || !_minds.TryGetMind(args.User, out var mind, out _) || mind != GetEntity(args.Mind))
            return;
        var panel = Comp<WiresPanelComponent>(apc);
        if (args.OpenPanel)
        {
            if (!panel.Open) _wires.TogglePanel(apc, panel, true, args.User);
            return;
        }
        if (!panel.Open) return;
        var container = _containers.EnsureContainer<ContainerSlot>(apc, ContainerId);
        if (!_containers.Insert(ent.Owner, container)) return;
        var installed = EnsureComp<OrbitraRatvarInstalledCogComponent>(apc);
        installed.Rule = rule;
        installed.NextExtraction = _timing.CurTime + ent.Comp.Interval;
        _wires.TogglePanel(apc, panel, false, args.User);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"Ratvar cult: {ToPrettyString(args.User)} installed {ToPrettyString(ent)} in {ToPrettyString(apc)} for {ToPrettyString(rule)}.");
        _popup.PopupEntity(Loc.GetString("orbitra-ratvar-cog-installed"), apc, args.User);
    }

    private EntityUid? GetCog(EntityUid apc) =>
        _containers.TryGetContainer(apc, ContainerId, out var container) && container.ContainedEntities.Count == 1
            ? container.ContainedEntities[0] : null;

    private void OnExamine(Entity<OrbitraRatvarInstalledCogComponent> ent, ref ExaminedEvent args)
    {
        if (TryComp<WiresPanelComponent>(ent, out var panel) && panel.Open && GetCog(ent) != null)
            args.PushMarkup(Loc.GetString("orbitra-ratvar-cog-examine"));
    }

    private void OnRemove(Entity<OrbitraRatvarInstalledCogComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<WiresPanelComponent>(ent, out var panel) || !panel.Open ||
            GetCog(ent) is not { } cog || !TryComp<OrbitraRatvarIntegrationCogComponent>(cog, out var comp)) return;
        args.Handled = _tools.UseTool(args.Used, args.User, ent, comp.RemoveDelay,
            [PryQuality], new OrbitraRatvarCogRemoveEvent { Cog = GetNetEntity(cog) }, out var id);
        TrackOperation(ent, id);
    }

    private void OnRemoveFinished(Entity<OrbitraRatvarInstalledCogComponent> ent, ref OrbitraRatvarCogRemoveEvent args)
    {
        ForgetOperation(ent, args.DoAfter.Id);
        if (args.Handled || args.Cancelled) return;
        args.Handled = true;
        if (!TryComp<WiresPanelComponent>(ent, out var panel) || !panel.Open ||
            GetCog(ent) is not { } cog || cog != GetEntity(args.Cog) ||
            args.Used is not { } tool || !_tools.HasQuality(tool, PryQuality) ||
            !_interaction.InRangeUnobstructed(args.User, ent.Owner)) return;
        QueueDel(cog);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"Ratvar cult: {ToPrettyString(args.User)} destroyed integration cog {ToPrettyString(cog)} in {ToPrettyString(ent)}.");
        RemCompDeferred<OrbitraRatvarInstalledCogComponent>(ent);
    }

    private void OnOperationsShutdown(Entity<OrbitraRatvarCogOperationsComponent> ent, ref ComponentShutdown args)
    {
        // Cancel синхронно вызывает обработчик завершения, который тоже удаляет запись.
        while (ent.Comp.Pending.Count > 0)
        {
            var index = ent.Comp.Pending.Count - 1;
            var id = ent.Comp.Pending[index];
            ent.Comp.Pending.RemoveAt(index);
            if (_doAfter.IsRunning(id))
                _doAfter.Cancel(id);
        }
    }

    private void TrackOperation(EntityUid apc, DoAfterId? id)
    {
        if (id is { } operation && _doAfter.IsRunning(operation))
            EnsureComp<OrbitraRatvarCogOperationsComponent>(apc).Pending.Add(operation);
    }

    private void ForgetOperation(EntityUid? apc, DoAfterId id)
    {
        if (apc is { } target && TryComp<OrbitraRatvarCogOperationsComponent>(target, out var operations))
            operations.Pending.Remove(id);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<OrbitraRatvarInstalledCogComponent>();
        while (query.MoveNext(out var uid, out var installed))
        {
            if (installed.NextExtraction > _timing.CurTime) continue;
            if (GetCog(uid) is not { } cog || !TryComp<OrbitraRatvarIntegrationCogComponent>(cog, out var settings))
            {
                RemCompDeferred<OrbitraRatvarInstalledCogComponent>(uid);
                continue;
            }
            if (!TryExtract((uid, installed)))
                installed.NextExtraction = _timing.CurTime + settings.Interval;
        }
    }

    /// <summary>Debits one capped extraction; failed checks never advance cult progression.</summary>
    public bool TryExtract(Entity<OrbitraRatvarInstalledCogComponent> apc)
    {
        if (!CanExtract(apc, out var settings, out var battery, out var rule, out var amount))
            return false;
        // Закрываем повторный вход до события изменения заряда батареи.
        apc.Comp.NextExtraction = _timing.CurTime + settings.Interval;
        if (!_battery.TryUseCharge((apc.Owner, battery), amount * settings.JoulesPerEnergy))
            return false;
        rule.Energy += amount;
        rule.Generated += amount;
        return true;
    }

    private bool CanExtract(Entity<OrbitraRatvarInstalledCogComponent> apc,
        out OrbitraRatvarIntegrationCogComponent settings, out BatteryComponent battery,
        out OrbitraRatvarRuleComponent rule, out int amount)
    {
        settings = default!;
        battery = default!;
        rule = default!;
        amount = 0;
        if (apc.Comp.NextExtraction > _timing.CurTime || TerminatingOrDeleted(apc) ||
            GetCog(apc) is not { } cog || TerminatingOrDeleted(cog) ||
            !TryComp<OrbitraRatvarIntegrationCogComponent>(cog, out var cogSettings) || cogSettings.Interval <= TimeSpan.Zero ||
            !HasComp<ApcComponent>(apc) || !Transform(apc).Anchored ||
            !TryComp<BatteryComponent>(apc, out var apcBattery) || cogSettings.JoulesPerEnergy <= 0 ||
            !TryComp<OrbitraRatvarRuleComponent>(apc.Comp.Rule, out var cult) || TerminatingOrDeleted(apc.Comp.Rule) || cult.Won || cult.Lost ||
            !_ticker.IsGameRuleActive(apc.Comp.Rule)) return false;
        settings = cogSettings;
        battery = apcBattery;
        rule = cult;
        var (charge, max) = _battery.GetCharge(apc.Owner);
        if (max <= 0 || charge < max * settings.MinimumChargeFraction) return false;
        amount = Math.Min(settings.EnergyPerInterval, Math.Min(rule.MaxEnergy - rule.Energy,
            (int) Math.Max(0, charge / settings.JoulesPerEnergy - settings.ReserveEnergy)));
        return amount > 0;
    }
}
