using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Mind;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.ActionBlocker;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Issues only configured gear after live authorization and atomic cooldown reservation.</summary>
public sealed partial class OrbitraRatvarCacheSystem : EntitySystem
{
    [Dependency] private OrbitraRatvarRuleSystem _cult = default!;
    [Dependency] private OrbitraRatvarPowerSystem _power = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarCacheComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<OrbitraRatvarCacheComponent, BeforeActivatableUIOpenEvent>(OnOpen);
        Subs.BuiEvents<OrbitraRatvarCacheComponent>(OrbitraRatvarCacheUiKey.Key,
            subs => subs.Event<OrbitraRatvarCacheMessage>(OnChoose));
    }

    private void OnOpenAttempt(Entity<OrbitraRatvarCacheComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!CanAccess(ent, args.User)) args.Cancel();
    }
    private void OnOpen(Entity<OrbitraRatvarCacheComponent> ent, ref BeforeActivatableUIOpenEvent args) => UpdateUi(ent, args.User);
    private void OnChoose(Entity<OrbitraRatvarCacheComponent> ent, ref OrbitraRatvarCacheMessage args)
    {
        TryIssue(ent, args.Actor, args.Choice);
        UpdateUi(ent, args.Actor);
    }

    /// <summary>Reserves the shared cooldown before spawning, preventing duplicate requests.</summary>
    public bool TryIssue(Entity<OrbitraRatvarCacheComponent> ent, EntityUid user, int choice)
    {
        if (!CanIssue(ent, user, choice, out _) ||
            !_power.TryUsePower((ent, Comp<OrbitraRatvarPoweredComponent>(ent)))) return false;
        ent.Comp.NextUse = _timing.CurTime + ent.Comp.Cooldown;
        var item = Spawn(ent.Comp.Choices[choice], Transform(ent).Coordinates);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"Ratvar Cache: {ToPrettyString(user)} created {ToPrettyString(item)} at {ToPrettyString(ent)}.");
        return true;
    }

    public bool CanIssue(Entity<OrbitraRatvarCacheComponent> ent, EntityUid user, int choice, out string reason)
    {
        reason = "orbitra-ratvar-cache-denied";
        if (choice < 0 || choice >= ent.Comp.Choices.Count || !CanAccess(ent, user)) return false;
        if (!TryComp<OrbitraRatvarPoweredComponent>(ent, out var powered) ||
            !_power.CanUsePower((ent, powered), out _, out var powerReason))
        {
            reason = "orbitra-ratvar-cache-unpowered";
            return false;
        }
        reason = "orbitra-ratvar-cache-cooldown";
        if (ent.Comp.NextUse > _timing.CurTime) return false;
        reason = "orbitra-ratvar-cache-ready";
        return true;
    }

    private bool CanAccess(EntityUid cache, EntityUid user) =>
        !TerminatingOrDeleted(cache) && !EntityManager.IsQueuedForDeletion(cache) &&
        TryComp<OrbitraRatvarStructureComponent>(cache, out var structure) &&
        _cult.TryGetCult(user, out var rule) && rule.Owner == structure.Rule &&
        TryComp<MobStateComponent>(user, out var mob) && mob.CurrentState == MobState.Alive &&
        _mind.TryGetMind(user, out _, out var mind) && mind.CurrentEntity == user &&
        !_container.IsEntityInContainer(cache) && !_container.IsEntityInContainer(user) &&
        _blocker.CanInteract(user, cache) && _interaction.InRangeUnobstructed(user, cache);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate) return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        var query = EntityQueryEnumerator<OrbitraRatvarCacheComponent>();
        while (query.MoveNext(out var uid, out var cache))
            foreach (var actor in _ui.GetActors(uid, OrbitraRatvarCacheUiKey.Key).ToArray())
                UpdateUi((uid, cache), actor);
    }

    private void UpdateUi(Entity<OrbitraRatvarCacheComponent> ent, EntityUid user)
    {
        if (!CanAccess(ent, user))
        {
            _ui.CloseUi(ent.Owner, OrbitraRatvarCacheUiKey.Key, user);
            return;
        }
        CanIssue(ent, user, 0, out var reason);
        _ui.SetUiState(ent.Owner, OrbitraRatvarCacheUiKey.Key, new OrbitraRatvarCacheUiState(reason,
            Math.Max(0, (int) Math.Ceiling((ent.Comp.NextUse - _timing.CurTime).TotalSeconds)),
            ent.Comp.Choices.Select(id => id.Id).ToArray()));
    }
}
