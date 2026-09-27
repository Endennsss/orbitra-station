using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Mind;
using Content.Shared.Players;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraRatvarSessionTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
        DummyTicker = false,
        NoLoadTestPrototypes = true,
    };

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReconnectPreservesMindAndCult(bool stopWhileDisconnected)
    {
        var map = await Pair.CreateTestMap();
        var originalSession = ServerSession!;
        var username = originalSession.Name;
        var userId = originalSession.UserId;
        EntityUid body = default;
        EntityUid mind = default;
        EntityUid rule = default;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(CultRule, out rule);
            body = SEntMan.SpawnEntity("OrbitraRatvarMarauder", map.GridCoords);
            SEntMan.GetComponent<OrbitraRatvarShellComponent>(body).Rule = rule;
            mind = Server.System<MindSystem>().CreateMind(userId);
            Server.System<MindSystem>().TransferTo(mind, body);
        });
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(originalSession.AttachedEntity, Is.EqualTo(body));
            Assert.That(Server.System<OrbitraRatvarRuleSystem>().TryGetCult(body, out _), Is.True);
        });

        var network = Client.ResolveDependency<IClientNetManager>();
        await Client.WaitPost(() => network.ClientDisconnect("Ratvar reconnect test"));
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(originalSession.Status, Is.EqualTo(SessionStatus.Disconnected));
            Assert.That(SEntMan.GetComponent<MindComponent>(mind).UserId, Is.EqualTo(userId));
            Assert.That(Server.System<RoleSystem>().MindHasRole<OrbitraRatvarRoleComponent>(mind), Is.True);
        });
        if (stopWhileDisconnected)
            await Server.WaitPost(() => Server.System<GameTicker>().EndGameRule(rule));

        Client.SetConnectTarget(Server);
        await Client.WaitPost(() => network.ClientConnect(null!, 0, username));
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            var session = Server.ResolveDependency<IPlayerManager>().Sessions.Single();
            Assert.That(session, Is.Not.SameAs(originalSession));
            Assert.That(session.Status, Is.EqualTo(SessionStatus.InGame));
            Assert.That(session.UserId, Is.EqualTo(userId));
            Assert.That(session.ContentData()!.Mind, Is.EqualTo(mind));
            Assert.That(session.AttachedEntity, Is.EqualTo(body));
            Assert.That(Server.System<OrbitraRatvarRuleSystem>().TryGetCult(body, out _), Is.EqualTo(!stopWhileDisconnected));
            Assert.That(SEntMan.HasComponent<OrbitraRatvarBodyComponent>(body), Is.EqualTo(!stopWhileDisconnected));
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            Assert.That(cult.Members, Is.EquivalentTo(new[] { mind }));
            Assert.That(cult.Converted, Is.Empty);
        });
    }

    [Test]
    public async Task RoundRestartClearsCultAndShells()
    {
        var map = await Pair.CreateTestMap();
        EntityUid rule = default;
        EntityUid body = default;
        EntityUid tablet = default;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(CultRule, out rule);
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            body = SEntMan.SpawnEntity("OrbitraRatvarMarauder", map.GridCoords);
            SEntMan.GetComponent<OrbitraRatvarShellComponent>(body).Rule = rule;
            Server.System<MindSystem>().TransferTo(Server.System<MindSystem>().CreateMind(ServerSession!.UserId), body);
            tablet = SEntMan.SpawnEntity("OrbitraRatvarTablet", map.GridCoords);
            cult.Energy = 1234;
            cult.Generated = 5678;
        });
        await Server.WaitPost(() => Server.System<GameTicker>().RestartRound());
        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(rule), Is.False);
            Assert.That(SEntMan.EntityExists(body), Is.False);
            Assert.That(SEntMan.EntityExists(tablet), Is.False);
            Assert.That(SEntMan.EntityQuery<OrbitraRatvarRuleComponent>(), Is.Empty);
            Assert.That(SEntMan.EntityQuery<OrbitraRatvarShellComponent>(), Is.Empty);
            Assert.That(SEntMan.EntityQuery<OrbitraRatvarBodyComponent>(), Is.Empty);
            Assert.That(SEntMan.EntityQuery<OrbitraRatvarRoleComponent>(), Is.Empty);
        });
        EntityUid nextRule = default;
        await Server.WaitPost(() => Server.System<GameTicker>().StartGameRule(CultRule, out nextRule));
        await Server.WaitAssertion(() =>
        {
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(nextRule);
            Assert.That(cult.Energy, Is.EqualTo(cult.StartingEnergy));
            Assert.That(cult.Generated, Is.Zero);
            Assert.That(cult.Members, Is.Empty);
            Assert.That(cult.Converted, Is.Empty);
            Assert.That(cult.HolyWaterSince, Is.Empty);
            Assert.That(cult.ProtectedUntil, Is.Empty);
        });
    }
}
