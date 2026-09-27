using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Checks live coverage, isolated accounting and the actual weapon firing path.</summary>
[TestFixture]
public sealed class OrbitraRatvarPowerTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [TestCase("ready", true)]
    [TestCase("boundary", true)]
    [TestCase("outside", false)]
    [TestCase("overlap", true)]
    [TestCase("empty", false)]
    [TestCase("foreign", false)]
    [TestCase("unanchor-sigil", false)]
    [TestCase("unanchor-device", false)]
    [TestCase("delete", false)]
    [TestCase("queue-delete", false)]
    [TestCase("queue-rule", false)]
    [TestCase("remove-component", false)]
    [TestCase("rebind", false)]
    [TestCase("other-grid", false)]
    [TestCase("ended", false)]
    [TestCase("won", false)]
    public async Task CoverageIsRevalidatedBeforeEachOperation(string scenario, bool expected)
    {
        var map = await Pair.CreateTestMap();
        var otherMap = await Pair.CreateTestMap();
        EntityUid rule = default, otherRule = default, sigil = default, device = default;
        OrbitraRatvarRuleComponent cult = default!, otherCult = default!;
        var success = false;
        await Server.WaitPost(() =>
        {
            var mapSystem = Server.System<SharedMapSystem>();
            for (var x = 0; x <= 8; x++)
                mapSystem.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            Server.System<GameTicker>().StartGameRule(CultRule, out rule);
            Server.System<GameTicker>().StartGameRule(CultRule, out otherRule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            otherCult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(otherRule);
            cult.Energy = scenario == "empty" ? 4 : 100;
            otherCult.Energy = 100;
            var origin = map.GridCoords.Offset(new Vector2(0.5f));
            var power = Server.System<OrbitraRatvarPowerSystem>();
            sigil = SpawnSigil(origin, scenario == "foreign" ? otherRule : rule);
            device = SEntMan.SpawnEntity("OrbitraRatvarTurret", origin.Offset(new Vector2(1, 0)));
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(device).Rule = rule;
            var transform = Server.System<SharedTransformSystem>();
            switch (scenario)
            {
                case "boundary":
                case "outside":
                    transform.Unanchor(device);
                    transform.SetCoordinates(device, origin.Offset(new Vector2(scenario == "boundary" ? 6 : 7, 0)));
                    transform.AnchorEntity(device);
                    break;
                case "overlap": SpawnSigil(origin.Offset(new Vector2(2, 0)), rule); break;
                case "unanchor-sigil": transform.Unanchor(sigil); break;
                case "unanchor-device": transform.Unanchor(device); break;
                case "delete": SEntMan.DeleteEntity(sigil); break;
                case "queue-delete": SEntMan.QueueDeleteEntity(sigil); break;
                case "queue-rule": SEntMan.QueueDeleteEntity(rule); break;
                case "remove-component": SEntMan.RemoveComponent<OrbitraRatvarTransmissionComponent>(sigil); break;
                case "rebind": power.BindTransmission((sigil, SEntMan.GetComponent<OrbitraRatvarTransmissionComponent>(sigil)), otherRule); break;
                case "other-grid":
                    transform.Unanchor(sigil);
                    transform.SetCoordinates(sigil, otherMap.GridCoords.Offset(new Vector2(0.5f)));
                    transform.AnchorEntity(sigil);
                    break;
                case "ended": Server.System<GameTicker>().EndGameRule(rule); break;
                case "won": cult.Won = true; break;
            }
            success = power.TryUsePower((device, SEntMan.GetComponent<OrbitraRatvarPoweredComponent>(device)));
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(success, Is.EqualTo(expected));
            Assert.That(cult.Energy, Is.EqualTo(scenario == "empty" ? 4 : expected ? 95 : 100));
            Assert.That(otherCult.Energy, Is.EqualTo(100));
            Assert.That(cult.Generated, Is.Zero);
            if (scenario is "delete" or "remove-component" or "rebind")
                Assert.That(cult.TransmissionSigils, Does.Not.Contain(sigil));
        });
    }

    [Test]
    public async Task TwoConsumersCannotSpendTheSameLastEnergy()
    {
        var map = await Pair.CreateTestMap();
        var results = new bool[2];
        OrbitraRatvarRuleComponent cult = default!;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = 5;
            SpawnSigil(map.GridCoords, rule);
            for (var i = 0; i < results.Length; i++)
            {
                var device = SEntMan.SpawnEntity("OrbitraRatvarTurret", map.GridCoords);
                SEntMan.GetComponent<OrbitraRatvarStructureComponent>(device).Rule = rule;
                results[i] = Server.System<OrbitraRatvarPowerSystem>().TryUsePower(
                    (device, SEntMan.GetComponent<OrbitraRatvarPoweredComponent>(device)));
            }
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(results, Is.EqualTo(new[] { true, false }));
            Assert.That(cult.Energy, Is.Zero);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GunPipelineCannotFireWithoutPower(bool powered)
    {
        var map = await Pair.CreateTestMap();
        OrbitraRatvarRuleComponent cult = default!;
        var fired = false;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = 5;
            if (powered) SpawnSigil(map.GridCoords, rule);
            var device = SEntMan.SpawnEntity("OrbitraRatvarTurret", map.GridCoords);
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(device).Rule = rule;
            fired = Server.System<GunSystem>().AttemptShoot(device,
                (device, SEntMan.GetComponent<GunComponent>(device)), map.GridCoords.Offset(new Vector2(3, 0)));
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(fired, Is.EqualTo(powered));
            Assert.That(cult.Energy, Is.EqualTo(powered ? 0 : 5));
        });
    }

    private EntityUid SpawnSigil(EntityCoordinates coordinates, EntityUid rule)
    {
        var sigil = SEntMan.SpawnEntity("OrbitraRatvarTransmissionSigil", coordinates);
        Server.System<OrbitraRatvarPowerSystem>().BindTransmission(
            (sigil, SEntMan.GetComponent<OrbitraRatvarTransmissionComponent>(sigil)), rule);
        return sigil;
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ObeliskAccessRequiresItsOwningCult(bool foreign)
    {
        var map = await Pair.CreateTestMap();
        var allowed = false;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            var userRule = rule;
            if (foreign) Server.System<GameTicker>().StartGameRule(CultRule, out userRule);
            SpawnSigil(map.GridCoords, rule);
            var obelisk = SEntMan.SpawnEntity("OrbitraRatvarObelisk", map.GridCoords);
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(obelisk).Rule = rule;
            var user = CreateCultist(map.GridCoords, userRule);
            allowed = Server.System<OrbitraRatvarRuleSystem>().CanCommunicate(
                (obelisk, SEntMan.GetComponent<OrbitraRatvarTabletComponent>(obelisk)), user, "Test", out _);
        });
        await Server.WaitAssertion(() => Assert.That(allowed, Is.EqualTo(!foreign)));
    }

    [TestCase("stays", true)]
    [TestCase("leaves", false)]
    [TestCase("foreign", false)]
    public async Task TransmissionRequiresOwnCultHelperAtStartAndCompletion(string scenario, bool expected)
    {
        var map = await Pair.CreateTestMap();
        EntityUid rule = default, helper = default;
        OrbitraRatvarRuleComponent cult = default!;
        var started = false;
        await Server.WaitPost(() =>
        {
            for (var x = 0; x <= 3; x++)
                Server.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            Server.System<GameTicker>().StartGameRule(CultRule, out rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            var user = CreateCultist(map.GridCoords.Offset(new Vector2(0.5f)), rule);
            var helperRule = rule;
            if (scenario == "foreign") Server.System<GameTicker>().StartGameRule(CultRule, out helperRule);
            helper = CreateCultist(map.GridCoords.Offset(new Vector2(1.5f, 0.5f)), helperRule);
            var tablet = SEntMan.SpawnEntity("OrbitraRatvarTablet", map.GridCoords);
            Server.System<SharedHandsSystem>().TryPickup(user, tablet);
            started = Server.System<OrbitraRatvarRuleSystem>().TryStartScripture(
                (tablet, SEntMan.GetComponent<OrbitraRatvarTabletComponent>(tablet)), user, "OrbitraRatvarTransmissionSigil");
        });
        await Server.WaitAssertion(() => Assert.That(started, Is.EqualTo(scenario != "foreign")));
        await Pair.RunSeconds(1);
        if (scenario == "leaves")
            await Server.WaitPost(() => Server.System<SharedTransformSystem>().SetCoordinates(helper,
                map.GridCoords.Offset(new Vector2(3.5f, 0.5f))));
        await Pair.RunSeconds(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(cult.TransmissionSigils.Count, Is.EqualTo(expected ? 1 : 0));
            Assert.That(cult.Energy, Is.EqualTo(expected ? 100 : 200));
        });
    }

    private EntityUid CreateCultist(EntityCoordinates coordinates, EntityUid rule)
    {
        var user = SEntMan.SpawnEntity("MobHuman", coordinates);
        var minds = Server.System<MindSystem>();
        var mind = minds.CreateMind(null);
        minds.TransferTo(mind, user);
        var roles = Server.System<RoleSystem>();
        roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
        roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
        role!.Value.Comp2.Rule = rule;
        SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Members.Add(mind);
        return user;
    }
}
