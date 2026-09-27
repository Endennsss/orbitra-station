using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Buckle.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarEminenceSystem
{
    // Переносы привязаны к проверенным станционным печатям, а не к координатам клиента.
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private OrbitraRatvarPowerSystem _power = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private void InitializeAbilities()
    {
        SubscribeLocalEvent<OrbitraRatvarEminenceAvatarComponent, OrbitraRatvarEminenceRecallEvent>(OnRecallFinished);
        SubscribeLocalEvent<OrbitraRatvarEminenceAvatarComponent, DoAfterAttemptEvent<OrbitraRatvarEminenceRecallEvent>>(OnRecallAttempt);
    }

    private void OnRecallAttempt(Entity<OrbitraRatvarEminenceAvatarComponent> ent,
        ref DoAfterAttemptEvent<OrbitraRatvarEminenceRecallEvent> args)
    {
        var operation = (OrbitraRatvarEminenceRecallEvent) args.DoAfter.Args.Event;
        if (!RecallMatches(ent, args.DoAfter.Args.User, operation) ||
            !CanRecall(ent, GetEntity(operation.Destination), out _, out _, out _)) args.Cancel();
    }

    private void OnRecallFinished(Entity<OrbitraRatvarEminenceAvatarComponent> ent, ref OrbitraRatvarEminenceRecallEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;
        if (TryComp<OrbitraRatvarEminenceComponent>(GetEntity(args.Observer), out var pending) && pending.RecallSequence == args.Sequence)
            pending.Recalling = false;
        if (args.Cancelled || !RecallMatches(ent, args.User, args) ||
            !CanRecall(ent, GetEntity(args.Destination), out var observer, out var target, out _)) return;
        var cult = Comp<OrbitraRatvarRuleComponent>(observer.Comp.Rule);
        // Все проверки повторены непосредственно перед единым списанием и переносом.
        cult.Energy -= ent.Comp.RecallEnergy;
        observer.Comp.RecallReadyAt = _timing.CurTime + ent.Comp.RecallCooldown;
        MoveRecalled(target, GetEntity(args.Destination));
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"Ratvar Eminence {ToPrettyString(ent)} recalled {ToPrettyString(target)}, spent {ent.Comp.RecallEnergy} for {ToPrettyString(observer.Comp.Rule)}.");
    }

    /// <summary>Starts a visible, interruptible seven-second recall of the currently observed member.</summary>
    public bool TryRecall(Entity<OrbitraRatvarEminenceAvatarComponent> avatar, EntityUid destination)
    {
        if (!CanRecall(avatar, destination, out var observer, out var target, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), avatar, avatar);
            return false;
        }
        if (observer.Comp.Recalling) return false;
        var operation = new OrbitraRatvarEminenceRecallEvent
        {
            Observer = GetNetEntity(observer), TargetMind = GetNetEntity(observer.Comp.TargetMind!.Value),
            Destination = GetNetEntity(destination),
            Sequence = ++observer.Comp.RecallSequence,
        };
        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, target, avatar.Comp.RecallDelay, operation, avatar)
            {
                NeedHand = false, BreakOnMove = true, BreakOnWeightlessMove = true, BreakOnDamage = true,
                AttemptFrequency = AttemptFrequency.EveryTick, DistanceThreshold = null,
            })) return false;
        observer.Comp.Recalling = true;
        _popup.PopupEntity(Loc.GetString("orbitra-ratvar-eminence-recall-warning"), target, target);
        return true;
    }

    /// <summary>Revalidates the native view, body, mind, movement attachments, shared resources and exact destination.</summary>
    public bool CanRecall(Entity<OrbitraRatvarEminenceAvatarComponent> avatar, EntityUid destination,
        out Entity<OrbitraRatvarEminenceComponent> observer, out EntityUid target, out string reason)
    {
        observer = default;
        target = default;
        reason = "orbitra-ratvar-eminence-ability-select";
        if (!CanAccessMenu(avatar, avatar, out var mind)) return false;
        observer = (mind, Comp<OrbitraRatvarEminenceComponent>(mind));
        if (observer.Comp.Target is not { } selected || !CanObserve(mind, selected) ||
            observer.Comp.TargetMind is not { } targetMind || !ControlledBy(targetMind, selected)) return false;
        target = selected;
        reason = "orbitra-ratvar-eminence-ability-cooldown";
        if (observer.Comp.RecallReadyAt > _timing.CurTime) return false;
        reason = "orbitra-ratvar-eminence-ability-energy";
        if (avatar.Comp.RecallEnergy < 0 || Comp<OrbitraRatvarRuleComponent>(observer.Comp.Rule).Energy < avatar.Comp.RecallEnergy) return false;
        reason = "orbitra-ratvar-eminence-ability-attached";
        if (!CanMoveMember(target, observer.Comp.Rule)) return false;
        return CanReceive(destination, observer.Comp.Rule, out reason);
    }

    /// <summary>Queues the once-per-cult recall to a selected powered waygate near its existing ark.</summary>
    public bool TryMassRecall(Entity<OrbitraRatvarEminenceAvatarComponent> avatar, EntityUid destination)
    {
        if (!CanMassRecall(avatar, destination, out var observer, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), avatar, avatar);
            return false;
        }
        Comp<OrbitraRatvarRuleComponent>(observer.Comp.Rule).EminenceRecallUsed = true;
        observer.Comp.MassRecallDestination = destination;
        observer.Comp.MassRecallAt = _timing.CurTime + avatar.Comp.MassRecallDelay;
        _ratvarRule.SendCultNotice(observer.Comp.Rule, Loc.GetString("orbitra-ratvar-eminence-mass-warning",
            ("seconds", (int) avatar.Comp.MassRecallDelay.TotalSeconds)));
        _adminLog.Add(LogType.Action, LogImpact.High, $"Ratvar Eminence {ToPrettyString(avatar)} initiated mass recall for {ToPrettyString(observer.Comp.Rule)} to {ToPrettyString(destination)}.");
        return true;
    }

    /// <summary>Checks the one-shot budget and station-local ark/waygate contract without changing them.</summary>
    public bool CanMassRecall(Entity<OrbitraRatvarEminenceAvatarComponent> avatar, EntityUid destination,
        out Entity<OrbitraRatvarEminenceComponent> observer, out string reason)
    {
        observer = default;
        reason = "orbitra-ratvar-eminence-ability-select";
        if (!CanAccessMenu(avatar, avatar, out var mind)) return false;
        observer = (mind, Comp<OrbitraRatvarEminenceComponent>(mind));
        if (observer.Comp.Target is not { } target || !CanObserve(mind, target)) return false;
        reason = "orbitra-ratvar-eminence-mass-used";
        if (Comp<OrbitraRatvarRuleComponent>(observer.Comp.Rule).EminenceRecallUsed) return false;
        return CanReceiveAtArk(destination, observer.Comp.Rule, avatar.Comp.ArkRange, out reason);
    }

    private bool RecallMatches(EntityUid avatar, EntityUid target, OrbitraRatvarEminenceRecallEvent operation) =>
        CanAccessMenu(avatar, avatar, out var mind) && mind == GetEntity(operation.Observer) &&
        TryComp<OrbitraRatvarEminenceComponent>(mind, out var observer) && observer.Target == target &&
        observer.RecallSequence == operation.Sequence &&
        observer.TargetMind == GetEntity(operation.TargetMind);

    private bool CanMoveMember(EntityUid body, EntityUid rule) =>
        !TerminatingOrDeleted(body) && !EntityManager.IsQueuedForDeletion(body) &&
        _mind.TryGetMind(body, out var mind, out _) && MemberOf(mind, rule) && ControlledBy(mind, body) &&
        TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Alive &&
        !HasComp<OrbitraRatvarEminenceAvatarComponent>(body) && !_containers.IsEntityInContainer(body) &&
        !Transform(body).Anchored &&
        !(TryComp<BuckleComponent>(body, out var buckle) && buckle.Buckled) &&
        !(TryComp<PullableComponent>(body, out var pullable) && pullable.BeingPulled) &&
        !(TryComp<PullerComponent>(body, out var puller) && puller.Pulling != null) &&
        Comp<OrbitraRatvarRuleComponent>(rule).Station is { } station &&
        _station.GetOwningStation(body) == station && _station.GetLargestGrid(station) is { } grid && Transform(body).GridUid == grid;

    private bool CanReceive(EntityUid destination, EntityUid rule, out string reason)
    {
        reason = "orbitra-ratvar-eminence-ability-destination";
        if (TerminatingOrDeleted(destination) || EntityManager.IsQueuedForDeletion(destination) ||
            !HasComp<OrbitraRatvarTravelComponent>(destination) ||
            !TryComp<OrbitraRatvarPoweredComponent>(destination, out var powered) ||
            !_power.CanUsePower((destination, powered), out var owner, out _) || owner.Owner != rule ||
            owner.Comp.Station is not { } station || _station.GetOwningStation(destination) != station ||
            _station.GetLargestGrid(station) is not { } grid || Transform(destination).GridUid != grid ||
            !TryComp<MapGridComponent>(grid, out var mapGrid) ||
            _map.GetTileRef(grid, mapGrid, Transform(destination).Coordinates).Tile.IsEmpty) return false;
        // Живые участники могут прибывать вместе; твёрдая геометрия и предметы блокируют приём.
        foreach (var entity in _lookup.GetEntitiesIntersecting(_transform.GetMapCoordinates(destination), LookupFlags.Static | LookupFlags.Dynamic))
        {
            if (entity != destination && !HasComp<MobStateComponent>(entity)) return false;
        }
        reason = "orbitra-ratvar-eminence-ability-ready";
        return true;
    }

    private bool CanReceiveAtArk(EntityUid destination, EntityUid rule, float range, out string reason)
    {
        reason = "orbitra-ratvar-eminence-mass-ark";
        if (Comp<OrbitraRatvarRuleComponent>(rule).Ark is not { } ark || TerminatingOrDeleted(ark) ||
            EntityManager.IsQueuedForDeletion(ark) || !TryComp<OrbitraRatvarStructureComponent>(ark, out var structure) ||
            structure.Rule != rule || !structure.Ark || !Transform(ark).Anchored || _containers.IsEntityInContainer(ark) ||
            TerminatingOrDeleted(destination) || Transform(ark).GridUid != Transform(destination).GridUid ||
            !_transform.InRange(Transform(ark).Coordinates, Transform(destination).Coordinates, range)) return false;
        return CanReceive(destination, rule, out reason);
    }

    private void MoveRecalled(EntityUid target, EntityUid destination)
    {
        _transform.SetCoordinates(target, Transform(destination).Coordinates);
        _transform.AttachToGridOrMap(target);
    }

    private void UpdateAbilities()
    {
        var query = EntityQueryEnumerator<OrbitraRatvarEminenceComponent>();
        while (query.MoveNext(out var mind, out var observer))
        {
            if (observer.MassRecallAt is not { } deadline || _timing.CurTime < deadline) continue;
            observer.MassRecallAt = null;
            var destination = observer.MassRecallDestination;
            observer.MassRecallDestination = null;
            if (!TryComp<MindComponent>(mind, out var data) || data.OwnedEntity is not { } avatar ||
                !CanAccessMenu(avatar, avatar, out _) || !TryComp<OrbitraRatvarEminenceAvatarComponent>(avatar, out var settings) ||
                observer.Target is not { } viewed || !CanObserve(mind, viewed) ||
                destination is not { } point || !CanReceiveAtArk(point, observer.Rule, settings.ArkRange, out _)) continue;
            // Снимок исключает изменение коллекции при событиях переноса; обход только одного культа.
            foreach (var member in new List<EntityUid>(Comp<OrbitraRatvarRuleComponent>(observer.Rule).Members))
            {
                if (TryComp<MindComponent>(member, out var target) && target.OwnedEntity is { } body &&
                    CanMoveMember(body, observer.Rule) && CanReceiveAtArk(point, observer.Rule, settings.ArkRange, out _))
                    MoveRecalled(body, point);
            }
        }
    }
}
