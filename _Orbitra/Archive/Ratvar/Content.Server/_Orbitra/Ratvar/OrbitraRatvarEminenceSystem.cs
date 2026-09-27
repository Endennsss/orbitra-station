using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Station;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Authorizes unique, revocable native views without granting body control or remote interactions.</summary>
public sealed partial class OrbitraRatvarEminenceSystem : EntitySystem
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private RoleSystem _roles = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    private readonly Dictionary<EntityUid, EntityUid> _reservations = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarEminenceComponent, ComponentShutdown>(OnShutdown);
        InitializeInvitations();
        InitializeUi();
        InitializeView();
        InitializeSummoning();
        InitializeAbilities();
    }

    public override void Shutdown()
    {
        _reservations.Clear();
        _invitations.Clear();
        _summons.Clear();
        base.Shutdown();
    }

    private void OnShutdown(Entity<OrbitraRatvarEminenceComponent> ent, ref ComponentShutdown args)
    {
        if (_reservations.TryGetValue(ent.Comp.Rule, out var owner) && owner == ent.Owner)
            _reservations.Remove(ent.Comp.Rule);
        ClearSelection(ent);
    }

    /// <summary>Reserves an existing member's mind; does not create a cult or add an antagonist role.</summary>
    public bool TryReserve(EntityUid mind, EntityUid rule)
    {
        if (!CanReserve(mind, rule)) return false;
        var component = EnsureComp<OrbitraRatvarEminenceComponent>(mind);
        component.Rule = rule;
        _reservations[rule] = mind;
        return true;
    }

    /// <summary>Only one existing member may hold a cult's slot, including while disconnected.</summary>
    public bool CanReserve(EntityUid mind, EntityUid rule)
    {
        if (!MemberOf(mind, rule) || !TryComp<OrbitraRatvarRuleComponent>(rule, out var cult) ||
            cult.Won || cult.Lost || !_ticker.IsGameRuleActive(rule)) return false;
        if (TryComp<OrbitraRatvarEminenceComponent>(mind, out var existing) && existing.Rule != rule) return false;
        if (_summons.ContainsKey(rule)) return false;
        if (_invitations.TryGetValue(rule, out var invitation) &&
            (!TryComp<OrbitraRatvarEminenceInvitationComponent>(invitation, out var pending) || pending.ClaimMind != mind))
            return false;
        return !_reservations.TryGetValue(rule, out var owner) || owner == mind;
    }

    /// <summary>Selects a body; acquired Eminence avatars receive a native camera, never possession.</summary>
    public bool TrySelect(EntityUid observerMind, EntityUid target)
    {
        if (!CanObserve(observerMind, target) || !TryComp<OrbitraRatvarEminenceComponent>(observerMind, out var observer))
            return false;
        StopView((observerMind, observer));
        observer.Recalling = false;
        observer.RecallSequence++;
        observer.ObserverBody = Comp<MindComponent>(observerMind).OwnedEntity;
        observer.Target = target;
        _mind.TryGetMind(target, out var targetMind, out _);
        observer.TargetMind = targetMind;
        StartView((observerMind, observer));
        return true;
    }

    /// <summary>Requires a controlled, living cult body with ordinary, unredirected vision.</summary>
    public bool CanObserve(EntityUid observerMind, EntityUid target)
    {
        if (!TryComp<OrbitraRatvarEminenceComponent>(observerMind, out var observer) ||
            !_reservations.TryGetValue(observer.Rule, out var reserved) || reserved != observerMind ||
            !CanReserve(observerMind, observer.Rule) ||
            !TryComp<MindComponent>(observerMind, out var mind) || mind.OwnedEntity is not { } observerBody ||
            !ControlledBy(observerMind, observerBody) || !ValidObserverBody(observerMind, observerBody) || target == observerBody ||
            !TryComp<MobStateComponent>(observerBody, out var observerState) || observerState.CurrentState != MobState.Alive ||
            !_mind.TryGetMind(target, out var targetMind, out _) || !MemberOf(targetMind, observer.Rule) ||
            !ControlledBy(targetMind, target) || HasComp<GhostComponent>(target) || _containers.IsEntityInContainer(target) ||
            !TryComp<MobStateComponent>(target, out var mob) || mob.CurrentState != MobState.Alive ||
            !HasUsableView(target) || TryComp<BlindableComponent>(target, out var blind) && blind.IsBlind)
            return false;

        var station = Comp<OrbitraRatvarRuleComponent>(observer.Rule).Station;
        if (station == null || TerminatingOrDeleted(station.Value) || _station.GetOwningStation(target) != station ||
            _station.GetLargestGrid(station.Value) is not { } grid) return false;
        return Transform(target).MapID == Transform(grid).MapID;
    }

    /// <summary>Revokes only this mind's observer reservation.</summary>
    public void Release(EntityUid mind)
    {
        if (!TryComp<OrbitraRatvarEminenceComponent>(mind, out var observer)) return;
        ClearSelection((mind, observer));
        RemComp<OrbitraRatvarEminenceComponent>(mind);
    }

    /// <summary>Clears selection without freeing a disconnected observer's unique slot.</summary>
    public void ClearSelection(Entity<OrbitraRatvarEminenceComponent> observer)
    {
        StopView(observer);
        observer.Comp.Recalling = false;
        observer.Comp.RecallSequence++;
        observer.Comp.MassRecallAt = null;
        observer.Comp.MassRecallDestination = null;
        observer.Comp.Target = null;
        observer.Comp.TargetMind = null;
        observer.Comp.ObserverBody = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateInvitations();
        UpdateSummoning();
        UpdateAbilities();
        var query = EntityQueryEnumerator<OrbitraRatvarEminenceComponent>();
        while (query.MoveNext(out var uid, out var observer))
        {
            if (!CanReserve(uid, observer.Rule))
            {
                ClearSelection((uid, observer));
                RemCompDeferred<OrbitraRatvarEminenceComponent>(uid);
                continue;
            }
            if (observer.Target is not { } target) continue;
            if (!CanObserve(uid, target) || Comp<MindComponent>(uid).OwnedEntity != observer.ObserverBody ||
                !_mind.TryGetMind(target, out var targetMind, out _) || targetMind != observer.TargetMind)
                ClearSelection((uid, observer));
            else
                UpdateView((uid, observer));
        }
        UpdateMenus();
    }

    private bool MemberOf(EntityUid mind, EntityUid rule) => !TerminatingOrDeleted(mind) &&
        !TerminatingOrDeleted(rule) && !EntityManager.IsQueuedForDeletion(mind) &&
        !EntityManager.IsQueuedForDeletion(rule) &&
        TryComp<OrbitraRatvarRuleComponent>(rule, out var cult) && cult.Members.Contains(mind) &&
        _roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role) && role.Value.Comp2.Rule == rule;

    private bool ValidObserverBody(EntityUid mind, EntityUid body) => !HasComp<GhostComponent>(body) &&
        _roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role) &&
        (!role.Value.Comp2.Eminence || HasComp<OrbitraRatvarEminenceAvatarComponent>(body));

    private bool ControlledBy(EntityUid mind, EntityUid body) => !TerminatingOrDeleted(body) &&
        !EntityManager.IsQueuedForDeletion(body) && TryComp<MindComponent>(mind, out var data) &&
        data.OwnedEntity == body && data.VisitingEntity == null && TryComp<ActorComponent>(body, out var actor) &&
        actor.PlayerSession.Status == SessionStatus.InGame && actor.PlayerSession.AttachedEntity == body &&
        actor.PlayerSession.UserId == data.UserId;
}
