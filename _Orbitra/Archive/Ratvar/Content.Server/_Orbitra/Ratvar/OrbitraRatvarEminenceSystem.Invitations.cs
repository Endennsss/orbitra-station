using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Database;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Server.Administration.Logs;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarEminenceSystem
{
    // Получение гостроли отделено от обзора и способностей наблюдателя.
    [Dependency] private GhostRoleSystem _ghostRole = default!;
    [Dependency] private OrbitraRatvarRuleSystem _ratvarRule = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    private static readonly EntProtoId InvitationPrototype = "OrbitraRatvarEminenceInvitation";
    private static readonly EntProtoId AvatarPrototype = "OrbitraRatvarEminenceAvatar";
    private readonly Dictionary<EntityUid, EntityUid> _invitations = [];

    private void InitializeInvitations()
    {
        SubscribeLocalEvent<OrbitraRatvarEminenceInvitationComponent, TakeGhostRoleEvent>(OnTakeInvitation);
        SubscribeLocalEvent<OrbitraRatvarEminenceInvitationComponent, ComponentShutdown>(OnInvitationShutdown);
        SubscribeLocalEvent<OrbitraRatvarEminenceAvatarComponent, MindRemovedMessage>(OnAvatarMindRemoved);
    }

    private void OnTakeInvitation(Entity<OrbitraRatvarEminenceInvitationComponent> ent, ref TakeGhostRoleEvent args)
    {
        if (!args.TookRole)
            args.TookRole = TryTakeInvitation(ent, args.Player);
    }

    private void OnInvitationShutdown(Entity<OrbitraRatvarEminenceInvitationComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Rule is { } rule && _invitations.TryGetValue(rule, out var pending) && pending == ent.Owner)
            _invitations.Remove(rule);
    }

    private void OnAvatarMindRemoved(Entity<OrbitraRatvarEminenceAvatarComponent> ent, ref MindRemovedMessage args)
    {
        if (TryComp<OrbitraRatvarEminenceComponent>(args.Mind.Owner, out var observer))
            ClearSelection((args.Mind.Owner, observer));
        QueueDel(ent);
    }

    /// <summary>Creates an immediate test-only invitation, bypassing the normal beacon countdown.</summary>
    public bool TryCreateTestInvitation(EntityUid user, out EntityUid invitation)
    {
        invitation = default;
        if (!CanCreateTestInvitation(user, out var cult)) return false;

        invitation = Spawn(InvitationPrototype, Transform(user).Coordinates);
        var pending = Comp<OrbitraRatvarEminenceInvitationComponent>(invitation);
        pending.Rule = cult.Owner;
        _invitations.Add(cult.Owner, invitation);
        EntityManager.AddComponents(invitation, pending.RoleComponents);
        _adminLog.Add(LogType.Mind, LogImpact.Medium,
            $"Ratvar test: {ToPrettyString(user)} created Eminence invitation {ToPrettyString(invitation)} for {ToPrettyString(cult)}.");
        return true;
    }

    /// <summary>Pending invitations and acquired minds share a cult's single slot.</summary>
    public bool CanCreateTestInvitation(EntityUid user, out Entity<OrbitraRatvarRuleComponent> cult)
    {
        cult = default;
        return !TerminatingOrDeleted(user) && !EntityManager.IsQueuedForDeletion(user) &&
            _ratvarRule.TryGetCult(user, out cult) && cult.Comp.TestTier != null &&
            !TerminatingOrDeleted(cult) && !EntityManager.IsQueuedForDeletion(cult) &&
            _mind.TryGetMind(user, out var mind, out _) && ControlledBy(mind, user) &&
            TryComp<MobStateComponent>(user, out var mob) && mob.CurrentState == MobState.Alive &&
            !_containers.IsEntityInContainer(user) && Transform(user).MapUid != null &&
            !_reservations.ContainsKey(cult.Owner) && !_invitations.ContainsKey(cult.Owner) && !_summons.ContainsKey(cult.Owner);
    }

    /// <summary>Consumes a valid native ghost-role request without attaching to another cultist's body.</summary>
    private bool TryTakeInvitation(Entity<OrbitraRatvarEminenceInvitationComponent> invitation, ICommonSession player)
    {
        if (!CanTakeInvitation(invitation, player)) return false;
        var rule = invitation.Comp.Rule!.Value;
        invitation.Comp.Claiming = true;
        var ghostRole = Comp<GhostRoleComponent>(invitation);
        _ghostRole.UnregisterGhostRole((invitation, ghostRole));

        // Nullspace не добавляет станционные PVS-чанки. Прототип не наследует привилегии обычного призрака.
        var avatar = Spawn(AvatarPrototype, MapCoordinates.Nullspace);
        _ghostRole.GhostRoleInternalCreateMindAndTransfer(player, invitation, avatar, ghostRole);
        if (!_mind.TryGetMind(avatar, out var mind, out _) ||
            !_roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var membership))
        {
            QueueDel(avatar);
            QueueDel(invitation);
            return false;
        }

        membership.Value.Comp2.Rule = rule;
        Comp<OrbitraRatvarRuleComponent>(rule).Members.Add(mind);
        invitation.Comp.ClaimMind = mind;
        var reserved = TryReserve(mind, rule);
        if (!reserved)
        {
            _roles.MindRemoveRole<OrbitraRatvarRoleComponent>(mind);
            QueueDel(avatar);
        }
        QueueDel(invitation);
        return reserved;
    }

    private bool CanTakeInvitation(Entity<OrbitraRatvarEminenceInvitationComponent> invitation, ICommonSession player) =>
        !TerminatingOrDeleted(invitation) && !EntityManager.IsQueuedForDeletion(invitation) &&
        !invitation.Comp.Claiming && invitation.Comp.Rule is { } rule &&
        _invitations.TryGetValue(rule, out var pending) && pending == invitation.Owner &&
        !_reservations.ContainsKey(rule) && ValidInvitation(invitation.Comp) &&
        TryComp<GhostRoleComponent>(invitation, out var role) && !role.Taken && !MetaData(invitation).EntityPaused &&
        player.Status == SessionStatus.InGame && player.AttachedEntity is { } ghost && HasComp<GhostComponent>(ghost) &&
        !TerminatingOrDeleted(ghost) && !EntityManager.IsQueuedForDeletion(ghost);

    private bool ValidInvitationCult(EntityUid rule) => !TerminatingOrDeleted(rule) &&
        !EntityManager.IsQueuedForDeletion(rule) && TryComp<OrbitraRatvarRuleComponent>(rule, out var cult) &&
        !cult.Won && !cult.Lost && _ticker.IsGameRuleActive(rule);

    private bool ValidInvitation(OrbitraRatvarEminenceInvitationComponent invitation) =>
        invitation.Rule is { } rule && ValidInvitationCult(rule) &&
        (invitation.TestOnly ? Comp<OrbitraRatvarRuleComponent>(rule).TestTier != null :
            invitation.Spire is { } spire && ValidSpire(spire, rule) && invitation.ExpiresAt > _timing.CurTime);

    private void UpdateInvitations()
    {
        var query = EntityQueryEnumerator<OrbitraRatvarEminenceInvitationComponent>();
        while (query.MoveNext(out var uid, out var invitation))
        {
            if (invitation.Rule == null || ValidInvitation(invitation)) continue;
            if (TryComp<GhostRoleComponent>(uid, out var role))
                _ghostRole.UnregisterGhostRole((uid, role));
            QueueDel(uid);
        }
    }
}
