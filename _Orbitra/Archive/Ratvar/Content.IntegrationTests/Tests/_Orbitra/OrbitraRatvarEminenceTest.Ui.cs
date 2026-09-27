using System.Linq;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using ClientUi = Robust.Client.GameObjects.UserInterfaceSystem;
using ServerUi = Robust.Server.GameObjects.UserInterfaceSystem;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed partial class OrbitraRatvarEminenceTest
{
    [Test]
    public async Task MenuNetworkSelectionAndClearUseOnlyAuthorizedView()
    {
        var mind = await AcquireInvitation(await PrepareInvitation());
        EntityUid avatar = default;
        NetEntity netAvatar = default, netTargetMind = default, netTarget = default;
        await Server.WaitPost(() =>
        {
            avatar = SEntMan.GetComponent<MindComponent>(mind).OwnedEntity!.Value;
            netAvatar = SEntMan.GetNetEntity(avatar);
            netTargetMind = SEntMan.GetNetEntity(_targetMind);
            netTarget = SEntMan.GetNetEntity(_target);
        });
        await Pair.RunTicksSync(15);
        await Client.WaitAssertion(() =>
            Assert.That(Client.System<ClientUi>().TryGetOpenUi(CEntMan.GetEntity(netAvatar), OrbitraRatvarEminenceUiKey.Key, out _), Is.True));
        await Client.WaitPost(() =>
        {
            Client.System<ClientUi>().TryGetOpenUi(CEntMan.GetEntity(netAvatar), OrbitraRatvarEminenceUiKey.Key, out var ui);
            ui!.SendMessage(new OrbitraRatvarEminenceSelectMessage(netTargetMind));
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEminenceComponent>(mind).Target, Is.EqualTo(_target));
            Assert.That(System.BuildMenuState(avatar, avatar)!.Selected, Is.EqualTo(netTargetMind));
            Assert.That(ServerSession!.ViewSubscriptions, Is.EquivalentTo(new[] { _target }));
            Assert.That(SEntMan.GetComponent<EyeComponent>(avatar).Target, Is.EqualTo(_target));
            Assert.That(ServerSession.AttachedEntity, Is.EqualTo(avatar));
        });
        await Client.WaitAssertion(() =>
        {
            var eye = CEntMan.GetComponent<EyeComponent>(CEntMan.GetEntity(netAvatar));
            Assert.That(eye.Target, Is.EqualTo(CEntMan.GetEntity(netTarget)));
            Assert.That(eye.DrawFov && eye.DrawLight, Is.True);
            Assert.That(eye.VisibilityMask, Is.EqualTo(EyeComponent.DefaultVisibilityMask));
        });
        await Client.WaitPost(() =>
        {
            Client.System<ClientUi>().TryGetOpenUi(CEntMan.GetEntity(netAvatar), OrbitraRatvarEminenceUiKey.Key, out var ui);
            ui!.SendMessage(new OrbitraRatvarEminenceClearMessage());
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEminenceComponent>(mind).Target, Is.Null);
            Assert.That(ServerSession!.ViewSubscriptions, Is.Empty);
            Assert.That(SEntMan.GetComponent<EyeComponent>(avatar).Target, Is.Null);
        });
        await Client.WaitAssertion(() =>
        {
            var body = CEntMan.GetEntity(netAvatar);
            Assert.That(CEntMan.GetComponent<EyeComponent>(body).Target, Is.Null);
            Assert.That(CEntMan.HasComponent<OrbitraRatvarEminenceViewComponent>(body), Is.False);
        });
    }

    [Test]
    public async Task MenuRejectsForeignStaleAndWrongActorRequests()
    {
        var mind = await AcquireInvitation(await PrepareInvitation());
        EntityUid avatar = default;
        OrbitraRatvarEminenceUiState state = null;
        bool foreign = true, stale = true, wrongActor = true;
        await Server.WaitPost(() =>
        {
            avatar = SEntMan.GetComponent<MindComponent>(mind).OwnedEntity!.Value;
            Server.System<GameTicker>().StartGameRule(Rule, out var otherRule);
            var roles = Server.System<RoleSystem>();
            roles.MindHasRole<OrbitraRatvarRoleComponent>(_targetMind, out var membership);
            membership!.Value.Comp2.Rule = otherRule;
            SEntMan.GetComponent<OrbitraRatvarRuleComponent>(otherRule).Members.Add(_targetMind);
            state = System.BuildMenuState(avatar, avatar);
            foreign = System.TrySelectMember(avatar, avatar, SEntMan.GetNetEntity(_targetMind));
            membership.Value.Comp2.Rule = _rule;
            wrongActor = System.TrySelectMember(avatar, _target, SEntMan.GetNetEntity(_targetMind));
            Server.System<MobStateSystem>().ChangeMobState(_target, MobState.Dead);
            stale = System.TrySelectMember(avatar, avatar, SEntMan.GetNetEntity(_targetMind));
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(state!.Members.Any(m => m.Mind == SEntMan.GetNetEntity(_targetMind)), Is.False);
            Assert.That(foreign || stale || wrongActor, Is.False);
            Assert.That(System.BuildMenuState(avatar, _target), Is.Null);
            Assert.That(System.BuildMenuState(avatar, avatar)!.Members.Single(m => m.Mind == SEntMan.GetNetEntity(_targetMind)).Availability,
                Is.EqualTo(OrbitraRatvarEminenceAvailability.NotAlive));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEminenceComponent>(mind).Target, Is.Null);
        });
    }

    [Test]
    public async Task MenuClosesOnRevocationAndDoesNotReopenAfterManualClose()
    {
        var mind = await AcquireInvitation(await PrepareInvitation());
        EntityUid avatar = default;
        var reopened = false;
        await Server.WaitPost(() =>
        {
            avatar = SEntMan.GetComponent<MindComponent>(mind).OwnedEntity!.Value;
            Server.System<ServerUi>().CloseUi(avatar, OrbitraRatvarEminenceUiKey.Key);
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() => Assert.That(Server.System<ServerUi>().IsUiOpen(avatar, OrbitraRatvarEminenceUiKey.Key), Is.False));
        await Server.WaitPost(() => reopened = System.TryOpenMenu(avatar));
        await Server.WaitAssertion(() => Assert.That(reopened, Is.True));
        await Server.WaitPost(() => Server.System<RoleSystem>().MindRemoveRole<OrbitraRatvarRoleComponent>(mind));
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<ServerUi>().IsUiOpen(avatar, OrbitraRatvarEminenceUiKey.Key), Is.False);
            Assert.That(System.BuildMenuState(avatar, avatar), Is.Null);
        });
    }

    [Test]
    public async Task MenuUnboundAvatarDoesNotGrantRoster()
    {
        await Prepare();
        EntityUid avatar = default;
        var opened = true;
        await Server.WaitPost(() =>
        {
            avatar = SEntMan.SpawnEntity(Avatar, _origin);
            opened = System.TryOpenMenu(avatar);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(opened, Is.False);
            Assert.That(System.BuildMenuState(avatar, avatar), Is.Null);
            Assert.That(System.BuildMenuState(avatar, _observer), Is.Null);
            Assert.That(Server.System<ServerUi>().IsUiOpen(avatar, OrbitraRatvarEminenceUiKey.Key), Is.False);
        });
    }
}
