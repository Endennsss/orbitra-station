using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mech.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraRatvarLensTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";
    private static readonly EntProtoId SlowStatus = "OrbitraRatvarInterdictionStatus";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [TestCase("ready", true)]
    [TestCase("boundary", true)]
    [TestCase("outside", false)]
    [TestCase("disabled", false)]
    [TestCase("empty", false)]
    [TestCase("coverage", false)]
    [TestCase("unanchored", false)]
    [TestCase("wall", false)]
    [TestCase("member", false)]
    [TestCase("other-cult", false)]
    [TestCase("dead", false)]
    [TestCase("container", false)]
    public async Task PaidSuppressionRevalidatesTargets(string scenario, bool expected)
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        OrbitraRatvarRuleComponent cult = default!;
        var result = false;
        var repeated = false;
        await Server.WaitPost(() =>
        {
            for (var x = -1; x <= 7; x++)
                Server.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            var origin = map.GridCoords.Offset(new Vector2(0.5f));
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = 100;
            var lens = SpawnLens(origin, rule);
            var user = CreateMember(origin, rule);
            var system = Server.System<OrbitraRatvarLensSystem>();
            if (scenario != "disabled") system.TryToggle(lens, user);
            target = SEntMan.SpawnEntity("MobHuman", origin.Offset(new Vector2(scenario == "outside" ? 6 : scenario == "boundary" ? 5 : 2, 0)));
            if (scenario == "member") AddMember(target, rule);
            if (scenario == "other-cult")
            {
                Server.System<GameTicker>().StartGameRule(CultRule, out var other);
                AddMember(target, other);
            }
            if (scenario == "dead")
                Server.System<DamageableSystem>().TryChangeDamage(target, new DamageSpecifier(SProtoMan.Index(Blunt), 500));
            if (scenario == "wall") SEntMan.SpawnEntity("WallSolid", origin.Offset(Vector2.UnitX));
            if (scenario == "container")
            {
                var box = SEntMan.SpawnEntity(null, origin.Offset(2 * Vector2.UnitX));
                var containers = Server.System<SharedContainerSystem>();
                containers.Insert(target, containers.EnsureContainer<ContainerSlot>(box, "test"));
            }
            if (scenario == "empty") cult.Energy = 5;
            if (scenario == "coverage") cult.TransmissionSigils.Clear();
            if (scenario == "unanchored") Server.System<SharedTransformSystem>().Unanchor(lens);
            lens.Comp.NextPulse = TimeSpan.Zero;
            result = system.TryPulse(lens);
            repeated = system.TryPulse(lens);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(repeated, Is.False);
            Assert.That(cult.Energy, Is.EqualTo(scenario == "empty" ? 5 : expected ? 95 : 100));
            Assert.That(Server.System<StatusEffectsSystem>().TryGetStatusEffect(target, SlowStatus, out _), Is.EqualTo(expected));
        });
    }

    [Test]
    public async Task LastEnergyFundsOnlyOneTargetAndStatusExpires()
    {
        var map = await Pair.CreateTestMap();
        EntityUid first = default, second = default, lensUid = default;
        OrbitraRatvarRuleComponent cult = default!;
        await Server.WaitPost(() =>
        {
            for (var x = 0; x <= 3; x++)
                Server.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            var origin = map.GridCoords.Offset(new Vector2(0.5f));
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = 6;
            var lens = SpawnLens(origin, rule);
            lensUid = lens;
            Server.System<OrbitraRatvarLensSystem>().TryToggle(lens, CreateMember(origin, rule));
            first = SEntMan.SpawnEntity("MobHuman", origin.Offset(Vector2.UnitX));
            second = SEntMan.SpawnEntity("MobHuman", origin.Offset(2 * Vector2.UnitX));
            lens.Comp.NextPulse = TimeSpan.Zero;
            Server.System<OrbitraRatvarLensSystem>().TryPulse(lens);
        });
        await Server.WaitAssertion(() =>
        {
            var statuses = Server.System<StatusEffectsSystem>();
            var count = (statuses.TryGetStatusEffect(first, SlowStatus, out _) ? 1 : 0) +
                        (statuses.TryGetStatusEffect(second, SlowStatus, out _) ? 1 : 0);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(cult.Energy, Is.EqualTo(1));
        });
        await Server.WaitPost(() => SEntMan.DeleteEntity(lensUid));
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<StatusEffectsSystem>().TryGetStatusEffect(first, SlowStatus, out _), Is.False);
            Assert.That(Server.System<StatusEffectsSystem>().TryGetStatusEffect(second, SlowStatus, out _), Is.False);
        });
    }

    [Test]
    public async Task MechTakesPaidDamageButForeignUserCannotToggle()
    {
        var map = await Pair.CreateTestMap();
        EntityUid mech = default;
        var toggled = true;
        var pulsed = false;
        var batteryDrain = 0f;
        OrbitraRatvarRuleComponent cult = default!;
        await Server.WaitPost(() =>
        {
            Server.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);
            var origin = map.GridCoords.Offset(new Vector2(0.5f));
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = 100;
            var lens = SpawnLens(origin, rule);
            var system = Server.System<OrbitraRatvarLensSystem>();
            var stranger = SEntMan.SpawnEntity("MobHuman", origin);
            toggled = system.TryToggle(lens, stranger);
            SEntMan.DeleteEntity(stranger);
            system.TryToggle(lens, CreateMember(origin, rule));
            mech = SEntMan.SpawnEntity("MechRipleyBattery", origin.Offset(Vector2.UnitX));
            var battery = SEntMan.GetComponent<MechComponent>(mech).BatterySlot.ContainedEntity!.Value;
            var batteries = Server.System<SharedBatterySystem>();
            var before = batteries.GetCharge(battery).Charge;
            lens.Comp.NextPulse = TimeSpan.Zero;
            pulsed = system.TryPulse(lens);
            batteryDrain = before - batteries.GetCharge(battery).Charge;
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(toggled, Is.False);
            Assert.That(pulsed, Is.True);
            Assert.That(cult.Energy, Is.EqualTo(95));
            Assert.That(SEntMan.GetComponent<MechComponent>(mech).Broken, Is.True);
            Assert.That(batteryDrain, Is.EqualTo(1000));
        });
    }

    [Test]
    public async Task OverlappingLensesRefreshOnePenaltyWithoutMultiplyingIt()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        OrbitraRatvarRuleComponent cult = default!;
        await Server.WaitPost(() =>
        {
            Server.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);
            var origin = map.GridCoords.Offset(new Vector2(0.5f));
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = 100;
            var user = CreateMember(origin, rule);
            var first = SpawnLens(origin, rule);
            var second = SpawnLens(origin, rule);
            var system = Server.System<OrbitraRatvarLensSystem>();
            system.TryToggle(first, user);
            system.TryToggle(second, user);
            target = SEntMan.SpawnEntity("MobHuman", origin.Offset(Vector2.UnitX));
            first.Comp.NextPulse = TimeSpan.Zero;
            second.Comp.NextPulse = TimeSpan.Zero;
            system.TryPulse(first);
            system.TryPulse(second);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(cult.Energy, Is.EqualTo(90));
            Assert.That(Server.System<StatusEffectsSystem>().TryGetStatusEffect(target, SlowStatus, out var status), Is.True);
            Assert.That(SEntMan.GetComponent<MovementModStatusEffectComponent>(status!.Value).WalkSpeedModifier, Is.EqualTo(0.5f));
            Assert.That(SEntMan.GetComponent<MovementSpeedModifierComponent>(target).WalkSpeedModifier, Is.EqualTo(0.5f));
        });
    }

    private Entity<OrbitraRatvarLensComponent> SpawnLens(EntityCoordinates coordinates, EntityUid rule)
    {
        var lens = SEntMan.SpawnEntity("OrbitraRatvarLens", coordinates);
        SEntMan.GetComponent<OrbitraRatvarStructureComponent>(lens).Rule = rule;
        var sigil = SEntMan.SpawnEntity("OrbitraRatvarTransmissionSigil", coordinates);
        Server.System<OrbitraRatvarPowerSystem>().BindTransmission(
            (sigil, SEntMan.GetComponent<OrbitraRatvarTransmissionComponent>(sigil)), rule);
        return (lens, SEntMan.GetComponent<OrbitraRatvarLensComponent>(lens));
    }

    private EntityUid CreateMember(EntityCoordinates coordinates, EntityUid rule)
    {
        var body = SEntMan.SpawnEntity("MobHuman", coordinates);
        AddMember(body, rule);
        return body;
    }

    private void AddMember(EntityUid body, EntityUid rule)
    {
        var minds = Server.System<MindSystem>();
        var mind = minds.CreateMind(null);
        minds.TransferTo(mind, body);
        var roles = Server.System<RoleSystem>();
        roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
        roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
        role!.Value.Comp2.Rule = rule;
        SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Members.Add(mind);
    }
}
