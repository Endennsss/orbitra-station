using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Mind;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Station;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Server-authoritative station travel with revalidation at both ends of the ritual.</summary>
public sealed partial class OrbitraRatvarTravelSystem : EntitySystem
{
    [Dependency] private OrbitraRatvarRuleSystem _cult = default!;
    [Dependency] private OrbitraRatvarPowerSystem _power = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private readonly HashSet<EntityUid> _points = [];
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarTravelComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<OrbitraRatvarTravelComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OrbitraRatvarTravelComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<OrbitraRatvarTravelComponent, BeforeActivatableUIOpenEvent>(OnOpen);
        SubscribeLocalEvent<OrbitraRatvarTravelComponent, OrbitraRatvarTravelEvent>(OnFinished);
        SubscribeLocalEvent<OrbitraRatvarTravelComponent, DoAfterAttemptEvent<OrbitraRatvarTravelEvent>>(OnAttempt);
        Subs.BuiEvents<OrbitraRatvarTravelComponent>(OrbitraRatvarTravelUiKey.Key, subs =>
        {
            subs.Event<OrbitraRatvarTravelMessage>(OnTravel);
            subs.Event<OrbitraRatvarTravelNameMessage>(OnName);
        });
    }

    public override void Shutdown()
    {
        _points.Clear();
        base.Shutdown();
    }

    private void OnStartup(Entity<OrbitraRatvarTravelComponent> ent, ref ComponentStartup args) => _points.Add(ent);
    private void OnShutdown(Entity<OrbitraRatvarTravelComponent> ent, ref ComponentShutdown args) => _points.Remove(ent);
    private void OnOpenAttempt(Entity<OrbitraRatvarTravelComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (CanAccess(ent, args.User, out _)) return;
        args.Cancel();
        if (HasMovementAttachment(args.User))
            _popup.PopupEntity(Loc.GetString("orbitra-ratvar-travel-attached"), args.User, args.User);
    }
    private void OnOpen(Entity<OrbitraRatvarTravelComponent> ent, ref BeforeActivatableUIOpenEvent args) => UpdateUi(ent, args.User);
    private void OnTravel(Entity<OrbitraRatvarTravelComponent> ent, ref OrbitraRatvarTravelMessage args)
    {
        TryStartTravel(ent, args.Actor, GetEntity(args.Destination));
        UpdateUi(ent, args.Actor);
    }
    private void OnName(Entity<OrbitraRatvarTravelComponent> ent, ref OrbitraRatvarTravelNameMessage args)
    {
        if (CanAccess(ent, args.Actor, out _) && !string.IsNullOrWhiteSpace(args.Name) &&
            args.Name.Length <= 40 && !args.Name.Any(char.IsControl))
            ent.Comp.Label = args.Name.Trim();
        UpdateUi(ent, args.Actor);
    }
    private void OnAttempt(Entity<OrbitraRatvarTravelComponent> ent, ref DoAfterAttemptEvent<OrbitraRatvarTravelEvent> args)
    {
        var operation = (OrbitraRatvarTravelEvent) args.DoAfter.Args.Event;
        if (!TryGetEntity(operation.Destination, out var destination) || destination is not { } target ||
            !CanTravel(ent, args.DoAfter.Args.User, target, out var rule) ||
            rule.Owner != GetEntity(operation.Rule) || !_mind.TryGetMind(args.DoAfter.Args.User, out var mind, out _) ||
            mind != GetEntity(operation.Mind)) args.Cancel();
    }
    private void OnFinished(Entity<OrbitraRatvarTravelComponent> ent, ref OrbitraRatvarTravelEvent args)
    {
        if (args.Handled || args.Cancelled) return;
        args.Handled = true;
        if (!TryGetEntity(args.Destination, out var destination) || destination is not { } target || !_mind.TryGetMind(args.User, out var mind, out _) ||
            mind != GetEntity(args.Mind) || !_cult.TryGetCult(args.User, out var rule) || rule.Owner != GetEntity(args.Rule)) return;
        TryTravel(ent, args.User, target);
    }

    /// <summary>Starts an interruptible journey; no resources are charged until arrival.</summary>
    public bool TryStartTravel(Entity<OrbitraRatvarTravelComponent> source, EntityUid user, EntityUid target)
    {
        if (HasMovementAttachment(user))
        {
            _popup.PopupEntity(Loc.GetString("orbitra-ratvar-travel-attached"), user, user);
            return false;
        }
        if (!CanTravel(source, user, target, out var rule) || !_mind.TryGetMind(user, out var mind, out _)) return false;
        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, source.Comp.Delay,
            new OrbitraRatvarTravelEvent { Rule = GetNetEntity(rule), Mind = GetNetEntity(mind), Destination = GetNetEntity(target) }, source)
        {
            BreakOnMove = true, BreakOnWeightlessMove = true, BreakOnDamage = true, NeedHand = false,
            AttemptFrequency = AttemptFrequency.EveryTick,
            DistanceThreshold = null,
        });
    }

    /// <summary>Commits one validated journey. Stale or foreign endpoints never spend energy.</summary>
    public bool TryTravel(Entity<OrbitraRatvarTravelComponent> source, EntityUid user, EntityUid target)
    {
        if (!CanTravel(source, user, target, out _) ||
            !_power.TryUsePower((source, Comp<OrbitraRatvarPoweredComponent>(source)))) return false;
        source.Comp.NextUse = _timing.CurTime + source.Comp.Cooldown;
        _transform.SetCoordinates(user, Transform(target).Coordinates);
        _transform.AttachToGridOrMap(user);
        _ui.CloseUi(source.Owner, OrbitraRatvarTravelUiKey.Key, user);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"Ratvar travel: {ToPrettyString(user)} travelled from {ToPrettyString(source)} to {ToPrettyString(target)}.");
        return true;
    }

    /// <summary>Checks ownership, living controlled body, power, station grid and exact arrival tile.</summary>
    public bool CanTravel(Entity<OrbitraRatvarTravelComponent> source, EntityUid user, EntityUid target,
        out Entity<OrbitraRatvarRuleComponent> rule)
    {
        if (!CanAccess(source, user, out rule) || source.Owner == target || source.Comp.NextUse > _timing.CurTime ||
            !CanUsePoint(source, rule) || !HasComp<OrbitraRatvarTravelComponent>(target) || !CanUsePoint(target, rule)) return false;
        // Никаких случайных соседних тайлов: занятый пункт отклоняется целиком.
        return !_lookup.AnyEntitiesIntersecting(_transform.GetMapCoordinates(target), LookupFlags.Static | LookupFlags.Dynamic);
    }

    private bool CanAccess(EntityUid source, EntityUid user, out Entity<OrbitraRatvarRuleComponent> rule)
    {
        rule = default;
        return !TerminatingOrDeleted(source) && !EntityManager.IsQueuedForDeletion(source) &&
            TryComp<OrbitraRatvarStructureComponent>(source, out var structure) &&
            _cult.TryGetCult(user, out rule) && structure.Rule == rule.Owner &&
            TryComp<MobStateComponent>(user, out var mob) && mob.CurrentState == MobState.Alive &&
            _mind.TryGetMind(user, out _, out var mind) && mind.CurrentEntity == user &&
            !Transform(user).Anchored && !HasMovementAttachment(user) &&
            !_container.IsEntityInContainer(user) && !_container.IsEntityInContainer(source) &&
            _blocker.CanInteract(user, source) && _interaction.InRangeUnobstructed(user, source);
    }

    private bool HasMovementAttachment(EntityUid user) =>
        TryComp<PullerComponent>(user, out var puller) && puller.Pulling != null ||
        TryComp<PullableComponent>(user, out var pullable) && pullable.BeingPulled ||
        TryComp<BuckleComponent>(user, out var buckle) && buckle.Buckled;

    private bool CanUsePoint(EntityUid point, Entity<OrbitraRatvarRuleComponent> rule)
    {
        if (TerminatingOrDeleted(point) || EntityManager.IsQueuedForDeletion(point) ||
            !TryComp<OrbitraRatvarPoweredComponent>(point, out var powered) ||
            !_power.CanUsePower((point, powered), out var owner, out _) || owner.Owner != rule.Owner ||
            rule.Comp.Station is not { } station || _station.GetOwningStation(point) != station ||
            Transform(point).GridUid is not { } grid || _station.GetLargestGrid(station) != grid ||
            !TryComp<MapGridComponent>(grid, out var mapGrid)) return false;
        return !_map.GetTileRef(grid, mapGrid, Transform(point).Coordinates).Tile.IsEmpty;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate) return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        foreach (var point in _points)
        {
            if (!TryComp<OrbitraRatvarTravelComponent>(point, out var component)) continue;
            foreach (var actor in _ui.GetActors(point, OrbitraRatvarTravelUiKey.Key).ToArray())
                UpdateUi((point, component), actor);
        }
    }

    private void UpdateUi(Entity<OrbitraRatvarTravelComponent> source, EntityUid user)
    {
        if (!CanAccess(source, user, out var rule))
        {
            _ui.CloseUi(source.Owner, OrbitraRatvarTravelUiKey.Key, user);
            return;
        }
        var destinations = new Dictionary<NetEntity, string>();
        foreach (var point in _points)
            if (CanTravel(source, user, point, out _) && TryComp<OrbitraRatvarTravelComponent>(point, out var destination))
                destinations[GetNetEntity(point)] = string.IsNullOrWhiteSpace(destination.Label) ? Name(point) : destination.Label;
        var ready = CanUsePoint(source, rule) && source.Comp.NextUse <= _timing.CurTime;
        _ui.SetUiState(source.Owner, OrbitraRatvarTravelUiKey.Key, new OrbitraRatvarTravelUiState(source.Comp.Label,
            rule.Comp.Energy, Comp<OrbitraRatvarPoweredComponent>(source).EnergyPerUse,
            ready ? "orbitra-ratvar-travel-ready" : "orbitra-ratvar-travel-unavailable", destinations));
    }
}
