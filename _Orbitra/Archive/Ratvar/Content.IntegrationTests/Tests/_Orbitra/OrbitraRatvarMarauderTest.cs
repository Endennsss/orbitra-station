using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraRatvarMarauderTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [Test]
    public async Task FourRealProjectilesAreAbsorbedAndFifthDamagesStandaloneBody()
    {
        var map = await Pair.CreateTestMap();
        EntityUid body = default, shooter = default;
        await Server.WaitPost(() =>
        {
            body = SEntMan.SpawnEntity("OrbitraRatvarMarauder", map.GridCoords.Offset(new Vector2(0.5f)));
            shooter = SEntMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(-1.5f, 0.5f)));
        });
        for (var shot = 0; shot < 5; shot++)
        {
            await Server.WaitPost(() =>
            {
                var bullet = SEntMan.SpawnEntity("BulletPistol", map.GridCoords.Offset(new Vector2(-0.5f, 0.5f)));
                Server.System<GunSystem>().ShootProjectile(bullet, Vector2.UnitX, Vector2.Zero, shooter, shooter, 4);
            });
            await Pair.RunSeconds(0.75f);
            var expected = Math.Max(0, 3 - shot);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<OrbitraRatvarMarauderComponent>(body).ShieldCharges, Is.EqualTo(expected));
                Assert.That(Server.System<DamageableSystem>().GetTotalDamage(body).Float(),
                    shot < 4 ? Is.EqualTo(0) : Is.GreaterThan(0));
                Assert.That(SEntMan.EntityQuery<OrbitraRatvarRuleComponent>(), Is.Empty);
            });
        }
    }

    [Test]
    public async Task RepeatedContactCannotConsumeTwoChargesAndMeleeIsNotBlocked()
    {
        var map = await Pair.CreateTestMap();
        EntityUid body = default;
        var first = false;
        var repeated = false;
        await Server.WaitPost(() =>
        {
            body = SEntMan.SpawnEntity("OrbitraRatvarMarauder", map.GridCoords);
            var bullet = SEntMan.SpawnEntity("BulletPistol", map.GridCoords);
            var system = Server.System<OrbitraRatvarMarauderSystem>();
            var marauder = SEntMan.GetComponent<OrbitraRatvarMarauderComponent>(body);
            var projectile = SEntMan.GetComponent<ProjectileComponent>(bullet);
            first = system.TryBlockProjectile((body, marauder), (bullet, projectile));
            repeated = system.TryBlockProjectile((body, marauder), (bullet, projectile));
            Server.System<DamageableSystem>().TryChangeDamage(body, new DamageSpecifier(SProtoMan.Index(Blunt), 10));
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(repeated, Is.False);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarMarauderComponent>(body).ShieldCharges, Is.EqualTo(3));
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(body).Float(), Is.EqualTo(10));
        });
        await Pair.RunSeconds(61);
        await Server.WaitAssertion(() => Assert.That(
            SEntMan.GetComponent<OrbitraRatvarMarauderComponent>(body).ShieldCharges, Is.EqualTo(3)));
    }

    [TestCase("success")]
    [TestCase("parallel")]
    [TestCase("drop")]
    [TestCase("off")]
    [TestCase("death")]
    public async Task WeldingRestoresOneChargeAndTenDamageOrCancels(string scenario)
    {
        var map = await Pair.CreateTestMap();
        EntityUid body = default, user = default, welder = default;
        var started = false;
        var secondStarted = false;
        await Server.WaitPost(() =>
        {
            var center = map.GridCoords.Offset(new Vector2(0.5f));
            body = SEntMan.SpawnEntity("OrbitraRatvarMarauder", center);
            user = SEntMan.SpawnEntity("MobHuman", center.Offset(-Vector2.UnitX));
            welder = SEntMan.SpawnEntity("Welder", center);
            Server.System<SharedHandsSystem>().TryPickup(user, welder);
            Server.System<ItemToggleSystem>().Toggle(welder, user);
            var marauder = SEntMan.GetComponent<OrbitraRatvarMarauderComponent>(body);
            marauder.ShieldCharges = 0;
            Server.System<DamageableSystem>().TryChangeDamage(body, new DamageSpecifier(SProtoMan.Index(Blunt), 30));
            var system = Server.System<OrbitraRatvarMarauderSystem>();
            started = system.TryStartRepair((body, marauder), user, welder);
            if (scenario == "parallel")
                secondStarted = system.TryStartRepair((body, marauder), user, welder);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(secondStarted, Is.False);
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitPost(() =>
        {
            if (scenario == "drop")
                Server.System<SharedHandsSystem>().TryDrop(user, welder);
            if (scenario == "off")
                Server.System<ItemToggleSystem>().Toggle(welder, user);
            if (scenario == "death")
                Server.System<DamageableSystem>().TryChangeDamage(body, new DamageSpecifier(SProtoMan.Index(Blunt), 500));
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            var success = scenario is "success" or "parallel";
            var marauder = SEntMan.GetComponent<OrbitraRatvarMarauderComponent>(body);
            Assert.That(marauder.ShieldCharges, Is.EqualTo(success ? 1 : 0));
            Assert.That(marauder.RepairPending, Is.Null);
            if (scenario != "death")
                Assert.That(Server.System<DamageableSystem>().GetTotalDamage(body).Float(), Is.EqualTo(success ? 20 : 30));
            Assert.That(SEntMan.EntityQuery<OrbitraRatvarRuleComponent>(), Is.Empty);
        });
    }
}
