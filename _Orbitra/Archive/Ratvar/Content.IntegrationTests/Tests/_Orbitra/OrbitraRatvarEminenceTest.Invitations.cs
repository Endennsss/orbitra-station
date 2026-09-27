using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Ghost.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Enums;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed partial class OrbitraRatvarEminenceTest
{
    private static readonly EntProtoId Observer = GameTicker.ObserverPrototypeName;
    private static readonly EntProtoId Invitation = "OrbitraRatvarEminenceInvitation";
    private static readonly EntProtoId Avatar = "OrbitraRatvarEminenceAvatar";
    private static readonly EntProtoId Tablet = "OrbitraRatvarTablet";

    private async Task<EntityUid> PrepareInvitation()
    {
        await Prepare();
        EntityUid invitation = default;
        var created = false;
        await Server.WaitPost(() =>
        {
            SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).TestTier = 3;
            created = System.TryCreateTestInvitation(_target, out invitation);
        });
        await Server.WaitAssertion(() => Assert.That(created, Is.True));
        return invitation;
    }

    private async Task<EntityUid> AcquireInvitation(EntityUid invitation)
    {
        EntityUid acquired = default;
        await Server.WaitPost(() =>
        {
            var minds = Server.System<MindSystem>();
            minds.TransferTo(_observerMind, SEntMan.SpawnEntity(Observer, _origin));
            var identifier = SEntMan.GetComponent<GhostRoleComponent>(invitation).Identifier;
            Server.System<GhostRoleSystem>().Request(ServerSession!, identifier);
            minds.TryGetMind(ServerSession!.AttachedEntity!.Value, out acquired, out _);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<OrbitraRatvarEminenceComponent>(acquired), Is.True));
        return acquired;
    }

    [Test]
    public async Task InvitationAcquisitionIsUniqueAndDoesNotGrantGhostVision()
    {
        var invitation = await PrepareInvitation();
        uint identifier = default;
        await Server.WaitPost(() => identifier = SEntMan.GetComponent<GhostRoleComponent>(invitation).Identifier);
        await Server.WaitAssertion(() =>
        {
            Assert.That(System.CanCreateTestInvitation(_target, out _), Is.False);
            Assert.That(System.CanReserve(_observerMind, _rule), Is.False);
        });
        var mind = await AcquireInvitation(invitation);
        await Server.WaitPost(() => Server.System<GhostRoleSystem>().Request(ServerSession!, identifier));
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(invitation), Is.False);
            Assert.That(Server.System<RoleSystem>().MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role), Is.True);
            Assert.That(role!.Value.Comp2.Rule, Is.EqualTo(_rule));
            Assert.That(role.Value.Comp2.Eminence, Is.True);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Members, Does.Contain(mind));
            var avatar = SEntMan.GetComponent<MindComponent>(mind).OwnedEntity!.Value;
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(avatar).EntityPrototype!.ID, Is.EqualTo(Avatar.Id));
            Assert.That(SEntMan.GetComponent<TransformComponent>(avatar).MapID, Is.EqualTo(MapId.Nullspace));
            Assert.That(ServerSession!.AttachedEntity, Is.EqualTo(avatar));
            Assert.That(ServerSession.ViewSubscriptions, Is.Empty);
            Assert.That(SEntMan.HasComponent<GhostComponent>(avatar), Is.False);
            Assert.That(SEntMan.HasComponent<HandsComponent>(avatar), Is.False);
            Assert.That(System.CanCreateTestInvitation(_target, out _), Is.False);
            Assert.That(SEntMan.GetComponent<MindComponent>(_targetMind).OwnedEntity, Is.EqualTo(_target));
        });
    }

    [TestCase("delete")]
    [TestCase("queued")]
    [TestCase("end")]
    [TestCase("disable")]
    public async Task InvitationInvalidationRejectsStaleTakeover(string reason)
    {
        var invitation = await PrepareInvitation();
        var tookRole = true;
        await Server.WaitPost(() =>
        {
            var identifier = SEntMan.GetComponent<GhostRoleComponent>(invitation).Identifier;
            Server.System<MindSystem>().TransferTo(_observerMind, SEntMan.SpawnEntity(Observer, _origin));
            switch (reason)
            {
                case "delete": SEntMan.DeleteEntity(invitation); break;
                case "queued": SEntMan.QueueDeleteEntity(invitation); break;
                case "end": SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Lost = true; break;
                case "disable": SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).TestTier = null; break;
            }
            tookRole = Server.System<GhostRoleSystem>().Takeover(ServerSession!, identifier);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(tookRole, Is.False);
            Assert.That(SEntMan.EntityExists(invitation), Is.False);
            Assert.That(SEntMan.HasComponent<OrbitraRatvarEminenceComponent>(_observerMind), Is.False);
            if (reason is "delete" or "queued")
                Assert.That(System.CanCreateTestInvitation(_target, out _), Is.True);
        });
    }

    [Test]
    public async Task InvitationRejectsLivingPlayerAndUnboundAdminSpawns()
    {
        var invitation = await PrepareInvitation();
        bool tookRole = true;
        EntityUid unbound = default, avatar = default;
        await Server.WaitPost(() =>
        {
            tookRole = Server.System<GhostRoleSystem>().Takeover(ServerSession!,
                SEntMan.GetComponent<GhostRoleComponent>(invitation).Identifier);
            unbound = SEntMan.SpawnEntity(Invitation, _origin);
            avatar = SEntMan.SpawnEntity(Avatar, MapCoordinates.Nullspace);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(tookRole, Is.False);
            Assert.That(SEntMan.GetComponent<GhostRoleComponent>(invitation).Taken, Is.False);
            Assert.That(SEntMan.HasComponent<GhostRoleComponent>(unbound), Is.False);
            Assert.That(SEntMan.HasComponent<OrbitraRatvarEminenceComponent>(avatar), Is.False);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEminenceInvitationComponent>(unbound).Rule, Is.Null);
        });
    }

    [Test]
    public async Task InvitationSlotSurvivesControlGapAndReattachment()
    {
        var invitation = await PrepareInvitation();
        var mind = await AcquireInvitation(invitation);
        EntityUid avatar = default;
        await Server.WaitPost(() =>
        {
            avatar = SEntMan.GetComponent<MindComponent>(mind).OwnedEntity!.Value;
            System.TrySelect(mind, _target);
            Server.System<MindSystem>().SetUserId(mind, null);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEminenceComponent>(mind).Target, Is.Null);
            Assert.That(System.CanCreateTestInvitation(_target, out _), Is.False);
        });
        await Server.WaitPost(() => Server.System<MindSystem>().SetUserId(mind, ServerSession!.UserId));
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(ServerSession!.AttachedEntity, Is.EqualTo(avatar));
            Assert.That(System.CanObserve(mind, _target), Is.True);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEminenceComponent>(mind).Target, Is.Null);
            Assert.That(System.CanCreateTestInvitation(_target, out _), Is.False);
        });
    }

    [Test]
    public async Task InvitationCultIsolationAndRoleRevocation()
    {
        var invitation = await PrepareInvitation();
        var mind = await AcquireInvitation(invitation);
        EntityUid other = default, secondInvitation = default, replacement = default;
        bool created = false;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(Rule, out other);
            SEntMan.GetComponent<OrbitraRatvarRuleComponent>(other).TestTier = 3;
            Server.System<RoleSystem>().MindHasRole<OrbitraRatvarRoleComponent>(_targetMind, out var role);
            role!.Value.Comp2.Rule = other;
            SEntMan.GetComponent<OrbitraRatvarRuleComponent>(other).Members.Add(_targetMind);
            created = System.TryCreateTestInvitation(_target, out secondInvitation);
            Server.System<RoleSystem>().MindRemoveRole<OrbitraRatvarRoleComponent>(mind);
            replacement = Server.System<MindSystem>().CreateMind(null);
            Bind(replacement, _rule);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEminenceInvitationComponent>(secondInvitation).Rule, Is.EqualTo(other));
            Assert.That(SEntMan.HasComponent<OrbitraRatvarEminenceComponent>(mind), Is.False);
            Assert.That(System.CanCreateTestInvitation(_target, out _), Is.False);
            Assert.That(System.CanReserve(replacement, _rule), Is.True);
        });
    }

    [Test]
    public async Task InvitationRoleCannotAcquireHumanScripturesAfterTransfer()
    {
        var invitation = await PrepareInvitation();
        var mind = await AcquireInvitation(invitation);
        EntityUid body = default, tablet = default;
        var canRecite = true;
        var ordinaryCultistCanRecite = false;
        await Server.WaitPost(() =>
        {
            body = SEntMan.SpawnEntity(Human, _origin);
            Server.System<MindSystem>().TransferTo(mind, body);
            tablet = SEntMan.SpawnEntity(Tablet, _origin);
            Server.System<SharedHandsSystem>().TryPickupAnyHand(body, tablet);
            canRecite = Server.System<OrbitraRatvarRuleSystem>().CanRecite(
                (tablet, SEntMan.GetComponent<OrbitraRatvarTabletComponent>(tablet)), body, "OrbitraRatvarBrass", out _, out _);
            Server.System<RoleSystem>().MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
            role!.Value.Comp2.Eminence = false;
            ordinaryCultistCanRecite = Server.System<OrbitraRatvarRuleSystem>().CanRecite(
                (tablet, SEntMan.GetComponent<OrbitraRatvarTabletComponent>(tablet)), body, "OrbitraRatvarBrass", out _, out _);
            role.Value.Comp2.Eminence = true;
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(canRecite, Is.False);
            Assert.That(ordinaryCultistCanRecite, Is.True);
            Assert.That(System.CanObserve(mind, _target), Is.False);
        });
    }

    [Test]
    public async Task InvitationRoleSurvivesNetworkReconnect()
    {
        var invitation = await PrepareInvitation();
        var mind = await AcquireInvitation(invitation);
        var session = ServerSession!;
        var name = session.Name;
        var user = session.UserId;
        var net = Client.ResolveDependency<IClientNetManager>();
        await Server.WaitPost(() => System.TrySelect(mind, _target));
        await Client.WaitPost(() => net.ClientDisconnect("Eminence reconnect regression"));
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(session.Status, Is.EqualTo(SessionStatus.Disconnected));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEminenceComponent>(mind).Target, Is.Null);
            Assert.That(System.CanCreateTestInvitation(_target, out _), Is.False);
        });
        Client.SetConnectTarget(Server);
        await Client.WaitPost(() => net.ClientConnect(null!, 0, name));
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.PlayerMan.TryGetSessionById(user, out var restored), Is.True);
            Assert.That(restored!.Status, Is.EqualTo(SessionStatus.InGame));
            Assert.That(restored.AttachedEntity, Is.EqualTo(SEntMan.GetComponent<MindComponent>(mind).OwnedEntity));
            Assert.That(restored.ViewSubscriptions, Is.Empty);
            Assert.That(Server.System<Robust.Server.GameObjects.UserInterfaceSystem>().IsUiOpen(
                restored.AttachedEntity!.Value, OrbitraRatvarEminenceUiKey.Key), Is.True);
            Assert.That(System.CanObserve(mind, _target), Is.True);
            Assert.That(System.CanCreateTestInvitation(_target, out _), Is.False);
        });
    }

    [Test]
    public async Task InvitationAvatarAllowsNormalGhostExit()
    {
        var invitation = await PrepareInvitation();
        var mind = await AcquireInvitation(invitation);
        var exited = false;
        EntityUid avatar = default;
        await Server.WaitPost(() =>
        {
            avatar = SEntMan.GetComponent<MindComponent>(mind).OwnedEntity!.Value;
            exited = Server.System<GhostSystem>().OnGhostAttempt(mind, true, viaCommand: true);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(exited, Is.True);
            Assert.That(SEntMan.EntityExists(avatar), Is.False);
            Assert.That(SEntMan.HasComponent<GhostComponent>(ServerSession!.AttachedEntity), Is.True);
            Assert.That(System.CanObserve(mind, _target), Is.False);
            Assert.That(System.CanCreateTestInvitation(_target, out _), Is.False);
        });
    }
}
