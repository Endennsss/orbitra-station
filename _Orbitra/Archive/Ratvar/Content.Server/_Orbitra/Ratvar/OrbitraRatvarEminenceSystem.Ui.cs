using System.Linq;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarEminenceSystem
{
    // Меню передаёт только состав своего культа; обзор выбранного тела выдаётся отдельно.
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    private TimeSpan _nextMenuUpdate;

    private void InitializeUi()
    {
        SubscribeLocalEvent<OrbitraRatvarEminenceAvatarComponent, OrbitraRatvarEminenceMenuEvent>(OnMenuAction);
        SubscribeLocalEvent<OrbitraRatvarEminenceAvatarComponent, PlayerDetachedEvent>(OnMenuDetached);
        SubscribeLocalEvent<OrbitraRatvarEminenceAvatarComponent, BoundUserInterfaceCheckRangeEvent>(OnMenuRange);
        SubscribeLocalEvent<OrbitraRatvarEminenceAvatarComponent, BoundUserInterfaceMessageAttempt>(OnMenuAttempt);
        Subs.BuiEvents<OrbitraRatvarEminenceAvatarComponent>(OrbitraRatvarEminenceUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnMenuOpened);
            subs.Event<OrbitraRatvarEminenceSelectMessage>(OnMenuSelect);
            subs.Event<OrbitraRatvarEminenceClearMessage>(OnMenuClear);
            subs.Event<OrbitraRatvarEminenceRecallMessage>(OnMenuRecall);
            subs.Event<OrbitraRatvarEminenceMassRecallMessage>(OnMenuMassRecall);
            subs.Event<OrbitraRatvarEminenceRealityMessage>(OnMenuReality);
        });
    }

    private void OnMenuAction(Entity<OrbitraRatvarEminenceAvatarComponent> ent, ref OrbitraRatvarEminenceMenuEvent args)
    {
        if (!args.Handled && args.Performer == ent.Owner) args.Handled = TryOpenMenu(ent);
    }

    private void OnMenuDetached(Entity<OrbitraRatvarEminenceAvatarComponent> ent, ref PlayerDetachedEvent args)
    {
        ent.Comp.MenuOpened = false;
        _ui.CloseUi(ent.Owner, OrbitraRatvarEminenceUiKey.Key);
        if (_mind.TryGetMind(ent, out var mind, out _) && TryComp<OrbitraRatvarEminenceComponent>(mind, out var observer))
            ClearSelection((mind, observer));
    }

    private void OnMenuRange(Entity<OrbitraRatvarEminenceAvatarComponent> ent, ref BoundUserInterfaceCheckRangeEvent args)
    {
        if (!Equals(args.UiKey, OrbitraRatvarEminenceUiKey.Key)) return;
        // У собственного аватара нет карты: физическая дальность здесь неприменима.
        args.Result = CanAccessMenu(ent, args.Actor, out _) ? BoundUserInterfaceRangeResult.Pass : BoundUserInterfaceRangeResult.Fail;
    }

    private void OnMenuAttempt(Entity<OrbitraRatvarEminenceAvatarComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (Equals(args.UiKey, OrbitraRatvarEminenceUiKey.Key) && !CanAccessMenu(ent, args.Actor, out _)) args.Cancel();
    }

    private void OnMenuOpened(Entity<OrbitraRatvarEminenceAvatarComponent> ent, ref BoundUIOpenedEvent args) =>
        UpdateMenu(ent, args.Actor);

    private void OnMenuSelect(Entity<OrbitraRatvarEminenceAvatarComponent> ent, ref OrbitraRatvarEminenceSelectMessage args)
    {
        TrySelectMember(ent, args.Actor, args.Mind);
        UpdateMenu(ent, args.Actor);
    }

    private void OnMenuClear(Entity<OrbitraRatvarEminenceAvatarComponent> ent, ref OrbitraRatvarEminenceClearMessage args)
    {
        if (CanAccessMenu(ent, args.Actor, out var mind)) ClearSelection((mind, Comp<OrbitraRatvarEminenceComponent>(mind)));
        UpdateMenu(ent, args.Actor);
    }

    private void OnMenuRecall(Entity<OrbitraRatvarEminenceAvatarComponent> ent, ref OrbitraRatvarEminenceRecallMessage args)
    {
        if (CanAccessMenu(ent, args.Actor, out _) && TryGetEntity(args.Destination, out var destination) && destination is { } point)
            TryRecall(ent, point);
        UpdateMenu(ent, args.Actor);
    }

    private void OnMenuMassRecall(Entity<OrbitraRatvarEminenceAvatarComponent> ent, ref OrbitraRatvarEminenceMassRecallMessage args)
    {
        if (CanAccessMenu(ent, args.Actor, out _) && TryGetEntity(args.Destination, out var destination) && destination is { } point)
            TryMassRecall(ent, point);
        UpdateMenu(ent, args.Actor);
    }

    private void OnMenuReality(Entity<OrbitraRatvarEminenceAvatarComponent> ent, ref OrbitraRatvarEminenceRealityMessage args)
    {
        if (CanAccessMenu(ent, args.Actor, out _)) TryManipulate(ent, args.Effect);
        UpdateMenu(ent, args.Actor);
    }

    /// <summary>Opens only the controlled, reserved Eminence avatar's own menu.</summary>
    public bool TryOpenMenu(EntityUid avatar)
    {
        if (!CanAccessMenu(avatar, avatar, out _)) return false;
        UpdateMenu(avatar, avatar);
        return _ui.TryOpenUi(avatar, OrbitraRatvarEminenceUiKey.Key, avatar);
    }

    /// <summary>Revalidates membership and the current body, including stale and forged roster requests.</summary>
    public bool TrySelectMember(EntityUid avatar, EntityUid actor, NetEntity member)
    {
        if (!CanAccessMenu(avatar, actor, out var observerMind) || !TryGetEntity(member, out var memberUid) ||
            memberUid is not { } targetMind || !MemberOf(targetMind, Comp<OrbitraRatvarEminenceComponent>(observerMind).Rule) ||
            !TryComp<MindComponent>(targetMind, out var target) || target.OwnedEntity is not { } body)
            return false;
        return TrySelect(observerMind, body);
    }

    /// <summary>Produces a private roster without PVS overrides, target coordinates, inventories or role details.</summary>
    public OrbitraRatvarEminenceUiState? BuildMenuState(EntityUid avatar, EntityUid actor)
    {
        if (!CanAccessMenu(avatar, actor, out var mind)) return null;
        var observer = Comp<OrbitraRatvarEminenceComponent>(mind);
        var members = new List<OrbitraRatvarEminenceMember>();
        foreach (var member in Comp<OrbitraRatvarRuleComponent>(observer.Rule).Members)
        {
            if (member == mind || !MemberOf(member, observer.Rule) || !TryComp<MindComponent>(member, out var target)) continue;
            var name = target.CharacterName ?? string.Empty;
            if (target.OwnedEntity is { } body && !TerminatingOrDeleted(body)) name = Name(body);
            // Ограничиваем размер подписи; клиент выводит её без разбора разметки.
            name = name.Replace('\n', ' ').Replace('\r', ' ');
            if (name.Length > 80) name = name[..80];
            members.Add(new OrbitraRatvarEminenceMember(GetNetEntity(member), name, GetAvailability(mind, member, target)));
        }
        members.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
        var destinations = new List<OrbitraRatvarEminenceDestination>();
        var settings = Comp<OrbitraRatvarEminenceAvatarComponent>(avatar);
        var query = EntityQueryEnumerator<OrbitraRatvarTravelComponent, OrbitraRatvarStructureComponent>();
        while (query.MoveNext(out var uid, out var point, out var structure))
        {
            if (structure.Rule != observer.Rule || TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid)) continue;
            var name = string.IsNullOrWhiteSpace(point.Label) ? Name(uid) : point.Label;
            CanRecall((avatar, settings), uid, out _, out _, out var recallReason);
            CanMassRecall((avatar, settings), uid, out _, out var massReason);
            destinations.Add(new OrbitraRatvarEminenceDestination(GetNetEntity(uid), name, recallReason, massReason));
        }
        CanManipulate((avatar, settings), out _, out var realityReason);
        return new OrbitraRatvarEminenceUiState(members.ToArray(), GetNetEntity(observer.TargetMind))
        {
            Energy = Comp<OrbitraRatvarRuleComponent>(observer.Rule).Energy,
            RecallCost = settings.RecallEnergy,
            RecallCooldown = Math.Max(0, (int) Math.Ceiling((observer.RecallReadyAt - _timing.CurTime).TotalSeconds)),
            Recalling = observer.Recalling,
            RealityCost = settings.RealityEnergy,
            RealityCooldown = Math.Max(0, (int) Math.Ceiling((observer.RealityReadyAt - _timing.CurTime).TotalSeconds)),
            RealityReason = realityReason,
            Destinations = destinations.ToArray(),
        };
    }

    private bool CanAccessMenu(EntityUid avatar, EntityUid actor, out EntityUid mind)
    {
        mind = default;
        return avatar == actor && HasComp<OrbitraRatvarEminenceAvatarComponent>(avatar) &&
            _mind.TryGetMind(avatar, out mind, out _) && ControlledBy(mind, avatar) &&
            ValidObserverBody(mind, avatar) && TryComp<MobStateComponent>(avatar, out var mob) && mob.CurrentState == MobState.Alive &&
            _roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role) && role.Value.Comp2.Eminence &&
            TryComp<OrbitraRatvarEminenceComponent>(mind, out var observer) &&
            _reservations.TryGetValue(observer.Rule, out var owner) && owner == mind && CanReserve(mind, observer.Rule);
    }

    private OrbitraRatvarEminenceAvailability GetAvailability(EntityUid observer, EntityUid member, MindComponent mind)
    {
        if (mind.OwnedEntity is not { } body || TerminatingOrDeleted(body) || EntityManager.IsQueuedForDeletion(body))
            return OrbitraRatvarEminenceAvailability.NoBody;
        if (HasComp<GhostComponent>(body) || mind.VisitingEntity != null) return OrbitraRatvarEminenceAvailability.Unavailable;
        if (!ControlledBy(member, body)) return OrbitraRatvarEminenceAvailability.Offline;
        if (_containers.IsEntityInContainer(body)) return OrbitraRatvarEminenceAvailability.Container;
        if (!TryComp<MobStateComponent>(body, out var mob) || mob.CurrentState != MobState.Alive)
            return OrbitraRatvarEminenceAvailability.NotAlive;
        if (TryComp<BlindableComponent>(body, out var blind) && blind.IsBlind) return OrbitraRatvarEminenceAvailability.Blind;
        if (!HasUsableView(body)) return OrbitraRatvarEminenceAvailability.Unavailable;
        return CanObserve(observer, body) ? OrbitraRatvarEminenceAvailability.Ready : OrbitraRatvarEminenceAvailability.OutsideStation;
    }

    private void UpdateMenu(EntityUid avatar, EntityUid actor)
    {
        var state = BuildMenuState(avatar, actor);
        if (state == null)
        {
            _ui.CloseUi(avatar, OrbitraRatvarEminenceUiKey.Key, actor);
            return;
        }
        if (_ui.TryGetUiState<OrbitraRatvarEminenceUiState>(avatar, OrbitraRatvarEminenceUiKey.Key, out var old) &&
            old.Selected == state.Selected && old.Members.SequenceEqual(state.Members) &&
            old.Energy == state.Energy && old.RecallCost == state.RecallCost && old.RecallCooldown == state.RecallCooldown &&
            old.Recalling == state.Recalling && old.Destinations.SequenceEqual(state.Destinations) &&
            old.RealityCost == state.RealityCost && old.RealityCooldown == state.RealityCooldown && old.RealityReason == state.RealityReason) return;
        _ui.SetUiState(avatar, OrbitraRatvarEminenceUiKey.Key, state);
    }

    private void UpdateMenus()
    {
        var refresh = _timing.CurTime >= _nextMenuUpdate;
        if (refresh) _nextMenuUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        var query = EntityQueryEnumerator<OrbitraRatvarEminenceAvatarComponent>();
        while (query.MoveNext(out var uid, out var avatar))
        {
            if (!CanAccessMenu(uid, uid, out _))
            {
                _ui.CloseUi(uid, OrbitraRatvarEminenceUiKey.Key);
                continue;
            }
            if (!avatar.MenuOpened) avatar.MenuOpened = TryOpenMenu(uid);
            if (refresh && _ui.IsUiOpen(uid, OrbitraRatvarEminenceUiKey.Key)) UpdateMenu(uid, uid);
        }
    }
}
