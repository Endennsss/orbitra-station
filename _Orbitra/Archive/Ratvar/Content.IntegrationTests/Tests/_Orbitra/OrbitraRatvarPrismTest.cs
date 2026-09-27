using System.Numerics;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraRatvarPrismTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [TestCase("ready", true)]
    [TestCase("boundary", true)]
    [TestCase("uncovered", false)]
    [TestCase("empty", false)]
    [TestCase("foreign", false)]
    [TestCase("outside", false)]
    [TestCase("unanchored", false)]
    [TestCase("disabled", false)]
    [TestCase("dead", false)]
    [TestCase("queue-patient", false)]
    public async Task PulseRechecksAuthorityAndCannotRepeat(string scenario, bool expected)
    {
        var map = await Pair.CreateTestMap();
        EntityUid prism = default, patient = default;
        OrbitraRatvarRuleComponent cult = default!;
        var result = false;
        var repeated = false;
        var remainingDamage = 0f;
        await Server.WaitPost(() =>
        {
            for (var x = -1; x <= 5; x++)
                Server.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            var origin = map.GridCoords.Offset(new Vector2(0.5f));
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            Server.System<GameTicker>().StartGameRule(CultRule, out var otherRule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = scenario == "empty" ? 4 : 100;
            prism = SpawnPrism(origin, rule, scenario != "uncovered");
            patient = CreateCultist(origin.Offset(new Vector2(scenario == "outside" ? 5 : scenario == "boundary" ? 4 : 1, 0)),
                scenario == "foreign" ? otherRule : rule);
            var damage = Server.System<DamageableSystem>();
            damage.TryChangeDamage(patient, new DamageSpecifier(SProtoMan.Index(Blunt), scenario == "dead" ? 500 : 40));
            var component = SEntMan.GetComponent<OrbitraRatvarPrismComponent>(prism);
            var system = Server.System<OrbitraRatvarPrismSystem>();
            if (scenario == "unanchored") Server.System<SharedTransformSystem>().Unanchor(prism);
            if (scenario == "disabled") system.TryToggle((prism, component), patient);
            if (scenario == "queue-patient") SEntMan.QueueDeleteEntity(patient);
            component.NextPulse = TimeSpan.Zero;
            result = system.TryPulse((prism, component));
            repeated = system.TryPulse((prism, component));
            remainingDamage = damage.GetTotalDamage(patient).Float();
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(repeated, Is.False);
            Assert.That(cult.Energy, Is.EqualTo(scenario == "empty" ? 4 : expected ? 98 : 100));
            if (scenario != "dead")
                Assert.That(remainingDamage, Is.EqualTo(expected ? 20 : 40));
        });
    }

    [Test]
    public async Task CapturedToxinsAreBoundedAndReleasedOnceThroughDestruction()
    {
        var map = await Pair.CreateTestMap();
        EntityUid prism = default, patient = default;
        Entity<SolutionComponent> blood = default;
        FixedPoint2 stored = default, remaining = default;
        var repeatedRelease = true;
        await Server.WaitPost(() =>
        {
            for (var x = -1; x <= 5; x++)
                Server.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            var origin = map.GridCoords.Offset(new Vector2(0.5f));
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Energy = 100;
            prism = SpawnPrism(origin, rule);
            patient = CreateCultist(origin.Offset(Vector2.UnitX), rule);
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var bloodstream = SEntMan.GetComponent<BloodstreamComponent>(patient);
            solutions.TryGetSolution(patient, bloodstream.BloodSolutionName, out var solution, out _);
            blood = solution!.Value;
            solutions.TryAddReagent(blood, "Toxin", 7);
            var component = SEntMan.GetComponent<OrbitraRatvarPrismComponent>(prism);
            component.Capacity = 3;
            component.NextPulse = TimeSpan.Zero;
            Server.System<DamageableSystem>().TryChangeDamage(patient, new DamageSpecifier(SProtoMan.Index(Poison), 30));
            Server.System<OrbitraRatvarPrismSystem>().TryPulse((prism, component));
            stored = component.Captured.Volume;
            remaining = blood.Comp.Solution.GetTotalPrototypeQuantity("Toxin");
            var destruction = new DestructionEventArgs();
            SEntMan.EventBus.RaiseLocalEvent(prism, destruction);
            repeatedRelease = Server.System<OrbitraRatvarPrismSystem>().TryRelease((prism, component));
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(stored, Is.EqualTo(FixedPoint2.New(3)));
            Assert.That(remaining, Is.EqualTo(FixedPoint2.New(4)));
            Assert.That(repeatedRelease, Is.False);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarPrismComponent>(prism).Captured.Volume, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(SEntMan.EntityQuery<SmokeComponent>().Count(), Is.EqualTo(1));
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(patient).Float(), Is.Zero);
        });
    }

    [Test]
    public async Task FullReservoirFitsInNativeSmokeWithoutLoss()
    {
        var map = await Pair.CreateTestMap();
        var repeated = true;
        FixedPoint2 smokeVolume = default;
        await Server.WaitPost(() =>
        {
            var prism = SEntMan.SpawnEntity("OrbitraRatvarPrism", map.GridCoords);
            var component = SEntMan.GetComponent<OrbitraRatvarPrismComponent>(prism);
            component.Captured.AddReagent(new ReagentId("Toxin", null), 1000);
            var system = Server.System<OrbitraRatvarPrismSystem>();
            system.TryRelease((prism, component));
            repeated = system.TryRelease((prism, component));
            var query = SEntMan.EntityQueryEnumerator<SmokeComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                Server.System<SharedSolutionContainerSystem>().TryGetSolution(uid, SmokeComponent.SolutionName, out _, out var contents);
                smokeVolume += contents!.Volume;
            }
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(repeated, Is.False);
            Assert.That(smokeVolume, Is.EqualTo(FixedPoint2.New(1000)));
        });
    }

    [Test]
    public async Task HealthyPatientsDoNotCostEnergyAndForeignCultCannotToggle()
    {
        var map = await Pair.CreateTestMap();
        var toggled = false;
        var pulsed = true;
        OrbitraRatvarRuleComponent cult = default!;
        await Server.WaitPost(() =>
        {
            for (var x = -1; x <= 5; x++)
                Server.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            var origin = map.GridCoords.Offset(new Vector2(0.5f));
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            Server.System<GameTicker>().StartGameRule(CultRule, out var foreign);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = 100;
            var prism = SpawnPrism(origin, rule);
            CreateCultist(origin.Offset(Vector2.UnitX), rule);
            var outsider = CreateCultist(origin.Offset(-Vector2.UnitX), foreign);
            var component = SEntMan.GetComponent<OrbitraRatvarPrismComponent>(prism);
            component.NextPulse = TimeSpan.Zero;
            var system = Server.System<OrbitraRatvarPrismSystem>();
            toggled = system.TryToggle((prism, component), outsider);
            pulsed = system.TryPulse((prism, component));
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(toggled, Is.False);
            Assert.That(pulsed, Is.False);
            Assert.That(cult.Energy, Is.EqualTo(100));
        });
    }

    private EntityUid SpawnPrism(EntityCoordinates origin, EntityUid rule, bool coverage = true)
    {
        if (coverage)
        {
            var sigil = SEntMan.SpawnEntity("OrbitraRatvarTransmissionSigil", origin);
            Server.System<OrbitraRatvarPowerSystem>().BindTransmission(
                (sigil, SEntMan.GetComponent<OrbitraRatvarTransmissionComponent>(sigil)), rule);
        }
        var prism = SEntMan.SpawnEntity("OrbitraRatvarPrism", origin);
        SEntMan.GetComponent<OrbitraRatvarStructureComponent>(prism).Rule = rule;
        return prism;
    }

    private EntityUid CreateCultist(EntityCoordinates coordinates, EntityUid rule)
    {
        var body = SEntMan.SpawnEntity("MobHuman", coordinates);
        var minds = Server.System<MindSystem>();
        var mind = minds.CreateMind(null);
        minds.TransferTo(mind, body);
        var roles = Server.System<RoleSystem>();
        roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
        roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
        role!.Value.Comp2.Rule = rule;
        SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Members.Add(mind);
        return body;
    }
}
