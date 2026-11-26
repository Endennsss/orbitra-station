using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Surgery;
using Content.Server.Medical.HealthAnalyzer;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Orbitra.Surgery;

public sealed class OrbitraTraumaTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: orbitraTrauma
          id: OrbitraTestTrauma
          fracture: { threshold: 28, chance: 1 }
          internalBleeding: { threshold: 36, chance: 1 }
          shrapnel: { threshold: 22, chance: 1 }
          dismemberment: { threshold: 85, chance: 0 }
          fragmentProjectiles: [PelletClusterLethal]
          tickSeconds: 1
          bloodLoss: 4
          bandagedBloodLoss: 1
          dressingSeconds: 4

        - type: orbitraTrauma
          id: OrbitraTestSevereTrauma
          fracture: { threshold: 28, chance: 0 }
          internalBleeding: { threshold: 36, chance: 0 }
          shrapnel: { threshold: 22, chance: 1 }
          dismemberment: { threshold: 85, chance: 1 }
          fragmentProjectiles: [BulletMagnum]

        - type: damageModifierSet
          id: OrbitraTestImpactProtection
          coefficients:
            Blunt: 0.5
            Piercing: 0.5

        - type: entity
          id: OrbitraTestTraumaPatient
          parent: MobHuman
          components:
          - type: OrbitraSurgicalPatient
            traumaProfile: OrbitraTestTrauma
          - type: Damageable
            damageModifierSet: OrbitraTestImpactProtection

        - type: entity
          id: OrbitraTestSevereTraumaPatient
          parent: OrbitraTestTraumaPatient
          components:
          - type: OrbitraSurgicalPatient
            traumaProfile: OrbitraTestSevereTrauma
        """;

    [Test]
    public async Task TraumaUsesOnePostResistancePhysicalHit()
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default;
        await Server.WaitPost(() =>
        {
            patient = SEntMan.SpawnAttachedTo("OrbitraTestTraumaPatient", map.GridCoords);
            var damage = Server.System<DamageableSystem>();
            for (var i = 0; i < 3; i++)
                damage.TryChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Blunt"] = 30 } });
            damage.TryChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Heat"] = 36 } });
        });
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<OrbitraTraumaComponent>(patient), Is.False));

        await Server.WaitPost(() => Server.System<DamageableSystem>().TryChangeDamage(patient,
            new DamageSpecifier { DamageDict = { ["Blunt"] = 36 } }, ignoreResistances: true));
        await Server.WaitAssertion(() =>
        {
            var trauma = SEntMan.GetComponent<OrbitraTraumaComponent>(patient);
            Assert.That(trauma.Injuries.Count(pair => (pair.Value & SurgeryInjury.Fracture) != 0), Is.EqualTo(1));
            Assert.That(trauma.Injuries[SurgeryZone.Torso], Is.EqualTo(SurgeryInjury.InternalBleeding));
            var expected = Enum.GetValues<SurgeryZone>().ToDictionary(zone => zone, zone => trauma.Injuries.GetValueOrDefault(zone));
            foreach (var active in new[] { false, true })
            {
                var scan = Server.System<ServerHealthAnalyzerSystem>().GetHealthAnalyzerUiState(patient, active);
                Assert.That(scan.OrbitraTrauma, Is.EquivalentTo(expected));
            }
        });
    }

    [Test]
    public async Task OnlyQualifyingProjectileLeavesDeepFragment()
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default;
        await Server.WaitPost(() =>
        {
            patient = SEntMan.SpawnAttachedTo("OrbitraTestTraumaPatient", map.GridCoords);
            var fragment = SEntMan.Spawn("PelletClusterLethal", MapCoordinates.Nullspace);
            var other = SEntMan.Spawn("PelletClusterRubber", MapCoordinates.Nullspace);
            var trauma = Server.System<OrbitraTraumaSystem>();
            trauma.OnProjectileDamage(patient, other, new DamageSpecifier { DamageDict = { ["Piercing"] = 45 } });
            trauma.OnProjectileDamage(patient, fragment, new DamageSpecifier { DamageDict = { ["Piercing"] = 21 } });
            SEntMan.DeleteEntity(other);
            SEntMan.DeleteEntity(fragment);
        });
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<OrbitraTraumaComponent>(patient), Is.False));
        await Server.WaitPost(() =>
        {
            var fragment = SEntMan.Spawn("PelletClusterLethal", MapCoordinates.Nullspace);
            Server.System<OrbitraTraumaSystem>().OnProjectileDamage(patient, fragment,
                new DamageSpecifier { DamageDict = { ["Piercing"] = 22 } });
            SEntMan.DeleteEntity(fragment);
        });
        await Server.WaitAssertion(() =>
        {
            var injuries = SEntMan.GetComponent<OrbitraTraumaComponent>(patient).Injuries;
            Assert.That(injuries.Values.Single(), Is.EqualTo(SurgeryInjury.Shrapnel));
        });
    }

    [Test]
    public async Task ProjectileCollisionCreatesAFragmentAndSeversALimbOnlyAtThePostArmorThreshold()
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default;
        void Fire(int piercing)
        {
            var coords = SEntMan.GetComponent<TransformComponent>(patient).Coordinates;
            var bullet = SSpawnAtPosition("BulletMagnum", coords.Offset(new Vector2(-0.8f, 0)));
            SComp<ProjectileComponent>(bullet).Damage = new DamageSpecifier { DamageDict = { ["Piercing"] = piercing } };
            Server.System<SharedGunSystem>().ShootProjectile(bullet, Vector2.UnitX, Vector2.Zero, gunUid: null, speed: 6f);
        }

        await Server.WaitPost(() =>
        {
            patient = SSpawnAtPosition("OrbitraTestSevereTraumaPatient", map.GridCoords);
            Fire(168);
        });
        var flightTicks = (int) Math.Ceiling(0.4 * SGameTiming.TickRate);
        await Pair.RunTicksSync(flightTicks);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<OrbitraTraumaComponent>(patient).Injuries.Values.Single(), Is.EqualTo(SurgeryInjury.Shrapnel));
            var effects = Server.System<OrbitraTraumaSystem>();
            Assert.That(OrbitraTraumaEffectsSystem.Zones.Any(z => effects.GetInjuries(patient, z) == SurgeryInjury.Missing), Is.False);
        });
        await Server.WaitPost(() => Fire(170));
        await Pair.RunTicksSync(flightTicks);
        await Server.WaitAssertion(() =>
        {
            var effects = Server.System<OrbitraTraumaSystem>();
            var lost = OrbitraTraumaEffectsSystem.Zones.Single(z => effects.GetInjuries(patient, z) == SurgeryInjury.Missing);
            var detached = SEntMan.AllEntityQueryEnumerator<OrbitraDetachedLimbComponent>();
            var found = 0;
            while (detached.MoveNext(out var limb))
            {
                if (limb.OriginalOwner != patient)
                    continue;
                Assert.That(limb.Zone, Is.EqualTo(lost));
                found++;
            }
            Assert.That(found, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SplintReducesHandPenaltyUntilSurgeonSetsTheFracture()
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid splint = default;
        SurgeryZone arm = default;
        float untreatedDelay = 0, fracturedSpeed = 0, treatedSpeed = 0;
        var started = false;

        float GetDelay()
        {
            var delay = new OrbitraHandActionDelayEvent(1f);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref delay);
            return delay.Multiplier;
        }

        float GetSpeed()
        {
            var speed = new RefreshMovementSpeedModifiersEvent();
            SEntMan.EventBus.RaiseLocalEvent(patient, speed);
            return speed.WalkSpeedModifier;
        }

        await Server.WaitPost(() =>
        {
            patient = SEntMan.SpawnAttachedTo("OrbitraTestTraumaPatient", map.GridCoords);
            var doctor = SEntMan.SpawnAttachedTo("MobHuman", map.GridCoords.Offset(new Vector2(0.75f, 0)));
            var hands = Server.System<SharedHandsSystem>();
            hands.TryGetHand(patient, hands.GetActiveHand(patient), out var hand);
            arm = hand!.Value.Location == HandLocation.Left ? SurgeryZone.LeftArm : SurgeryZone.RightArm;

            var damage = Server.System<DamageableSystem>();
            for (var i = 0; i < 4; i++)
                damage.TryChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Blunt"] = 28 } }, ignoreResistances: true);

            var trauma = Server.System<OrbitraTraumaSystem>();
            untreatedDelay = GetDelay();
            fracturedSpeed = GetSpeed();
            trauma.TryTreat(patient, SurgeryZone.LeftLeg, SurgeryInjury.Fracture);
            trauma.TryTreat(patient, SurgeryZone.RightLeg, SurgeryInjury.Fracture);
            treatedSpeed = GetSpeed();
            trauma.TryTreat(patient, arm == SurgeryZone.LeftArm ? SurgeryZone.RightArm : SurgeryZone.LeftArm, SurgeryInjury.Fracture);

            splint = SEntMan.SpawnAttachedTo("OrbitraSplint", map.GridCoords);
            hands.TryPickup(doctor, splint);
            var apply = new AfterInteractEvent(doctor, splint, patient, map.GridCoords, true);
            SEntMan.EventBus.RaiseLocalEvent(splint, apply);
            started = apply.Handled;
        });
        Assert.That(started, Is.True);
        Assert.That(untreatedDelay, Is.EqualTo(1.6f));
        Assert.That(treatedSpeed, Is.GreaterThan(fracturedSpeed));
        await Pair.RunTicksSync((int) Math.Ceiling(3.2 * SGameTiming.TickRate));
        await Server.WaitAssertion(() =>
        {
            Assert.That(GetDelay(), Is.EqualTo(1.15f));
            Assert.That(SEntMan.EntityExists(splint), Is.False);
            Assert.That(Server.System<OrbitraTraumaSystem>().GetInjuries(patient, arm),
                Is.EqualTo(SurgeryInjury.Fracture | SurgeryInjury.Splinted));
        });
        await Server.WaitPost(() => Server.System<OrbitraTraumaSystem>().TryTreat(patient, arm, SurgeryInjury.Fracture));
        await Server.WaitAssertion(() => Assert.That(GetDelay(), Is.EqualTo(1f)));
    }

    [Test]
    public async Task GauzeRechecksIndicationAndTemporarilyReducesBloodLoss()
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid doctor = default;
        EntityUid gauze = default;
        int initialCount = 0;
        var held = false;
        var started = false;
        var use = () =>
        {
            var interaction = new AfterInteractEvent(doctor, gauze, patient, map.GridCoords, true);
            SEntMan.EventBus.RaiseLocalEvent(gauze, interaction);
            return interaction.Handled;
        };
        var hit = () => Server.System<DamageableSystem>().TryChangeDamage(patient,
            new DamageSpecifier { DamageDict = { ["Blunt"] = 36 } }, ignoreResistances: true);

        await Server.WaitPost(() =>
        {
            patient = SEntMan.SpawnAttachedTo("OrbitraTestTraumaPatient", map.GridCoords);
            doctor = SEntMan.SpawnAttachedTo("MobHuman", map.GridCoords.Offset(new Vector2(0.75f, 0)));
            gauze = SEntMan.SpawnAttachedTo("Gauze", map.GridCoords);
            held = Server.System<SharedHandsSystem>().TryPickup(doctor, gauze);
            initialCount = SEntMan.GetComponent<StackComponent>(gauze).Count;
            hit();
            started = use();
            Server.System<OrbitraTraumaSystem>().TryTreat(patient, SurgeryZone.Torso, SurgeryInjury.InternalBleeding);
        });
        Assert.That(held && started, Is.True);
        var actionTicks = (int) Math.Ceiling(3.2 * SGameTiming.TickRate);
        await Pair.RunTicksSync(actionTicks);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<StackComponent>(gauze).Count, Is.EqualTo(initialCount)));

        float initialBlood = 0;
        await Server.WaitPost(() =>
        {
            hit();
            started = use();
            initialBlood = Server.System<BloodstreamSystem>().GetBloodLevel(patient);
        });
        Assert.That(started, Is.True);
        await Pair.RunTicksSync(actionTicks);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<OrbitraTraumaSystem>().GetInjuries(patient, SurgeryZone.Torso),
                Is.EqualTo(SurgeryInjury.InternalBleeding | SurgeryInjury.Bandaged));
            Assert.That(SEntMan.GetComponent<StackComponent>(gauze).Count, Is.EqualTo(initialCount - 1));
            Assert.That(Server.System<BloodstreamSystem>().GetBloodLevel(patient), Is.LessThan(initialBlood));
        });
        await Pair.RunTicksSync((int) (6 * SGameTiming.TickRate));
        await Server.WaitAssertion(() => Assert.That(Server.System<OrbitraTraumaSystem>().GetInjuries(patient, SurgeryZone.Torso),
            Is.EqualTo(SurgeryInjury.InternalBleeding)));
    }
}
