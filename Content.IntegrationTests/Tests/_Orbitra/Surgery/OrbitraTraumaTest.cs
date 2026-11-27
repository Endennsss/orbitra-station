#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Surgery;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Medical.HealthAnalyzer;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Administration.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Flash;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
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
          explosionEarChance: 1
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

        - type: orbitraTrauma
          id: OrbitraTestTargetedOrganTrauma
          fracture: { threshold: 28, chance: 0 }
          internalBleeding: { threshold: 36, chance: 0 }
          shrapnel: { threshold: 22, chance: 0 }
          dismemberment: { threshold: 85, chance: 0 }
          fragmentProjectiles: [BulletMagnum]
          maxBallisticOrganThreshold: 10
          organInjury: { threshold: 10, chance: 1 }
          organFailure: { threshold: 16, chance: 1 }
          torsoOrganWeight: 1
          headOrganWeight: 0
          explosionEarChance: 1
          flashEyeDuration: 4
          flashEyeChance: 1

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

        - type: entity
          id: OrbitraTestTargetedOrganPatient
          parent: OrbitraTestTraumaPatient
          components:
          - type: OrbitraSurgicalPatient
            traumaProfile: OrbitraTestTargetedOrganTrauma

        - type: entity
          id: OrbitraTestPiercingProofVest
          parent: ClothingOuterArmorBulletproof
          components:
          - type: Armor
            modifiers:
              coefficients:
                Piercing: 0
        """;

    [Test]
    public async Task OrganTraumaUsesZoneWeightsAndFallsBackToTheOnlyEligibleZone()
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default;
        var hit = new DamageSpecifier { DamageDict = { ["Blunt"] = 20 } };
        var organs = Server.System<OrbitraOrganSystem>();
        var damage = Server.System<DamageableSystem>();
        await Server.WaitPost(() =>
        {
            patient = SSpawnAtPosition("OrbitraTestTargetedOrganPatient", map.GridCoords);
            damage.TryChangeDamage(patient, hit, interruptsDoAfters: false);
        });
        await Server.WaitAssertion(() => Assert.That(organs.GetConditions(patient).Values, Is.All.EqualTo(SurgeryOrganCondition.Healthy)));

        await Server.WaitPost(() => damage.TryChangeDamage(patient, hit));
        await Server.WaitAssertion(() =>
        {
            var injured = organs.GetConditions(patient).Single(x => x.Value == SurgeryOrganCondition.Damaged);
            Assert.That(OrbitraOrganSystem.ZoneFor(injured.Key), Is.EqualTo(SurgeryZone.Torso));
        });

        await Server.WaitPost(() =>
        {
            foreach (var kind in OrbitraOrganSystem.Kinds.Where(k => OrbitraOrganSystem.ZoneFor(k) == SurgeryZone.Torso))
                SComp<OrbitraOrganComponent>(organs.Installed(patient, kind)!.Value).Condition = SurgeryOrganCondition.Nonfunctional;
            damage.TryChangeDamage(patient, hit);
        });
        await Server.WaitAssertion(() =>
        {
            var injured = organs.GetConditions(patient).Single(x => x.Value == SurgeryOrganCondition.Damaged);
            Assert.That(OrbitraOrganSystem.ZoneFor(injured.Key), Is.EqualTo(SurgeryZone.Head));
        });
    }

    [Test]
    public async Task ExplosionRedirectsOnlySuccessfulOrganTraumaToEligibleEars()
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default, control = default;
        await Server.WaitPost(() =>
        {
            var center = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
            patient = SSpawnAtPosition("OrbitraTestTargetedOrganPatient", center);
            control = SSpawnAtPosition("OrbitraTestTraumaPatient", center);
            Server.System<ExplosionSystem>().QueueExplosion(patient, "Default", totalIntensity: 16, slope: 8,
                maxTileIntensity: 4, maxTileBreak: 0, canCreateVacuum: false);
        });
        await Pair.RunTicksSync((int) SGameTiming.TickRate);
        await Server.WaitAssertion(() =>
        {
            var organs = Server.System<OrbitraOrganSystem>();
            var damaged = organs.GetConditions(patient).Single(x => x.Value != SurgeryOrganCondition.Healthy);
            Assert.That(damaged.Key, Is.EqualTo(SurgeryOrgan.Ears));
            Assert.That(damaged.Value, Is.EqualTo(SurgeryOrganCondition.Damaged));
            var actual = Server.System<DamageableSystem>().GetPositiveDamage((control, SComp<DamageableComponent>(control)));
            Assert.That(actual.DamageDict["Blunt"].Float(), Is.Positive);
            Assert.That(organs.GetConditions(control).Values, Is.All.EqualTo(SurgeryOrganCondition.Healthy));
        });
    }

    [Test]
    public async Task OnlyAnUnprotectedFlashOfSufficientDurationDamagesHealthyEyes()
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default;
        var organs = Server.System<OrbitraOrganSystem>();
        var inventory = Server.System<InventorySystem>();
        void Flash(double seconds) => Server.System<SharedFlashSystem>().Flash(patient, patient, null,
            TimeSpan.FromSeconds(seconds), 0.8f, displayPopup: false);

        await Server.WaitPost(() =>
        {
            patient = SSpawnAtPosition("OrbitraTestTargetedOrganPatient", map.GridCoords);
            var glasses = SSpawnAtPosition("ClothingEyesGlassesSunglasses", map.GridCoords);
            inventory.TryEquip(patient, glasses, "eyes", force: true);
            Flash(4);
        });
        await Server.WaitAssertion(() => Assert.That(organs.Condition(patient, SurgeryOrgan.Eyes), Is.EqualTo(SurgeryOrganCondition.Healthy)));

        await Server.WaitPost(() =>
        {
            inventory.TryUnequip(patient, "eyes", force: true);
            Flash(3);
            Flash(3);
        });
        await Server.WaitAssertion(() => Assert.That(organs.Condition(patient, SurgeryOrgan.Eyes), Is.EqualTo(SurgeryOrganCondition.Healthy)));

        await Server.WaitPost(() =>
        {
            Flash(4);
            Flash(5);
        });
        await Server.WaitAssertion(() =>
        {
            var damaged = organs.GetConditions(patient).Single(x => x.Value != SurgeryOrganCondition.Healthy);
            Assert.That(damaged.Key, Is.EqualTo(SurgeryOrgan.Eyes));
            Assert.That(damaged.Value, Is.EqualTo(SurgeryOrganCondition.Damaged));
        });
    }

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
            var damage = Server.System<DamageableSystem>();
            damage.ChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Piercing"] = 22 } },
                ignoreResistances: true, projectile: other);
            damage.ChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Piercing"] = 21 } },
                ignoreResistances: true, projectile: fragment);
            SEntMan.DeleteEntity(other);
            SEntMan.DeleteEntity(fragment);
        });
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<OrbitraTraumaComponent>(patient), Is.False));
        await Server.WaitPost(() =>
        {
            var fragment = SEntMan.Spawn("PelletClusterLethal", MapCoordinates.Nullspace);
            Server.System<DamageableSystem>().ChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Piercing"] = 22 } },
                ignoreResistances: true, projectile: fragment);
            SEntMan.DeleteEntity(fragment);
        });
        await Server.WaitAssertion(() =>
        {
            var injuries = SEntMan.GetComponent<OrbitraTraumaComponent>(patient).Injuries;
            Assert.That(injuries.Values.Single(), Is.EqualTo(SurgeryInjury.Shrapnel));
        });
    }

    private async Task FireProjectile(EntityUid patient, string projectile)
    {
        await Server.WaitPost(() =>
        {
            var coords = SComp<TransformComponent>(patient).Coordinates;
            var bullet = SSpawnAtPosition(projectile, coords.Offset(new Vector2(-0.8f, 0)));
            Server.System<SharedGunSystem>().ShootProjectile(bullet, Vector2.UnitX, Vector2.Zero, gunUid: null, speed: 6f);
        });
        await Pair.RunTicksSync((int) Math.Ceiling(0.4 * SGameTiming.TickRate));
    }

    [Test]
    public async Task FifteenOrdinaryPistolHitsReliablyInjureHumanOrgans()
    {
        var map = await Pair.CreateTestMap();
        var patient = await SpawnAtPosition("MobHuman", map.GridCoords);
        var organs = Server.System<OrbitraOrganSystem>();
        var previous = 0;
        for (var shot = 1; shot <= 15; shot++)
        {
            await FireProjectile(patient, "BulletPistol");
            await Server.WaitAssertion(() =>
            {
                var damage = Server.System<DamageableSystem>().GetPositiveDamage((patient, SComp<DamageableComponent>(patient)));
                Assert.That(damage.DamageDict["Piercing"].Float(), Is.GreaterThanOrEqualTo(16 * shot));
                var conditions = organs.GetConditions(patient);
                Assert.That(conditions.Values, Does.Not.Contain(SurgeryOrganCondition.Missing));
                var severity = conditions.Values.Sum(value => (int) value);
                Assert.That(severity - previous, Is.InRange(0, 1));
                Assert.That(severity, Is.GreaterThanOrEqualTo(shot / 4));
                previous = severity;
            });
        }
    }

    [TestCase(null, "BulletPistol", 16f, 16f)]
    [TestCase("ClothingOuterArmorBulletproof", "BulletPistol", 6.4f, 6.4f)]
    [TestCase("OrbitraTestPiercingProofVest", "BulletPistol", 0f, 0f)]
    [TestCase(null, "BulletPistolIncendiary", 12f, 12f)]
    [TestCase(null, "BulletPistolUranium", 10f, 10f)]
    [TestCase(null, "BulletMinigun", 5f, 5f)]
    [TestCase(null, "BulletPistolPractice", 0f, 0f)]
    [TestCase(null, "ProjectileIcicle", 20f, 0f)]
    public async Task BallisticOrganTraumaUsesOnlyPiercingThatPassesWornArmor(string? vest, string projectile,
        float expectedPiercing, float expectedBallistic)
    {
        var map = await Pair.CreateTestMap();
        var patient = await SpawnAtPosition("MobHuman", map.GridCoords);
        var equipped = false;
        await Server.WaitPost(() =>
        {
            SComp<OrbitraHumanOrganPatientComponent>(patient).BallisticOrganThreshold = 16;
            if (vest != null)
                equipped = Server.System<InventorySystem>().TryEquip(patient, SSpawnAtPosition(vest, map.GridCoords),
                    "outerClothing", force: true);
        });
        Assert.That(vest == null || equipped, Is.True);

        await FireProjectile(patient, projectile);
        await Server.WaitAssertion(() =>
        {
            var damage = Server.System<DamageableSystem>().GetPositiveDamage((patient, SComp<DamageableComponent>(patient)));
            Assert.That(damage.DamageDict.GetValueOrDefault("Piercing").Float(), Is.EqualTo(expectedPiercing).Within(0.01));
            if (projectile == "BulletPistolPractice")
                Assert.That(damage.DamageDict.GetValueOrDefault("Blunt").Float(), Is.Positive);
            var organs = Server.System<OrbitraOrganSystem>().GetConditions(patient);
            var wounded = expectedBallistic >= 16;
            Assert.That(organs.Values.Count(v => v != SurgeryOrganCondition.Healthy), Is.EqualTo(wounded ? 1 : 0));
            Assert.That(SComp<OrbitraHumanOrganPatientComponent>(patient).BallisticDamage.Float(),
                Is.EqualTo(wounded ? 0f : expectedBallistic).Within(0.01));
            if (expectedBallistic < 8)
                Assert.That(SEntMan.HasComponent<OrbitraTraumaComponent>(patient), Is.False);
        });
    }

    [Test]
    public async Task PiercingTreatmentAndRejuvenationReduceAccumulatedBallisticDamage()
    {
        var map = await Pair.CreateTestMap();
        var patient = await SpawnAtPosition("MobHuman", map.GridCoords);
        var damage = Server.System<DamageableSystem>();
        OrbitraHumanOrganPatientComponent organs = null!;
        await Server.WaitPost(() =>
        {
            organs = SComp<OrbitraHumanOrganPatientComponent>(patient);
            organs.BallisticOrganThreshold = 64;
        });
        await FireProjectile(patient, "BulletPistol");
        await Server.WaitPost(() => damage.ChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Piercing"] = -4 } }));
        await Server.WaitAssertion(() =>
        {
            Assert.That(organs.BallisticDamage.Float(), Is.EqualTo(12));
            Assert.That(organs.BallisticOrganThreshold.Float(), Is.EqualTo(64));
        });
        await Server.WaitPost(() => damage.ChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Piercing"] = -20 } }));
        await Server.WaitAssertion(() =>
        {
            Assert.That(organs.BallisticDamage.Float(), Is.Zero);
            Assert.That(organs.BallisticOrganThreshold.Float(), Is.Zero);
        });
        await Server.WaitPost(() => organs.BallisticOrganThreshold = 64);
        await FireProjectile(patient, "BulletPistol");
        await Server.WaitPost(() => Server.System<RejuvenateSystem>().PerformRejuvenate(patient));
        await Server.WaitAssertion(() =>
        {
            Assert.That(organs.BallisticDamage.Float(), Is.Zero);
            Assert.That(organs.BallisticOrganThreshold.Float(), Is.Zero);
        });
    }

    [Test]
    public async Task AMagnumHitUsesExactlyOneOrganTraumaPath()
    {
        var map = await Pair.CreateTestMap();
        var patient = await SpawnAtPosition("OrbitraTestTargetedOrganPatient", map.GridCoords);
        await FireProjectile(patient, "BulletMagnum");
        await Server.WaitAssertion(() =>
        {
            var organs = Server.System<OrbitraOrganSystem>().GetConditions(patient);
            Assert.That(organs.Values.Count(value => value == SurgeryOrganCondition.Damaged), Is.EqualTo(1));
            Assert.That(organs.Values, Does.Not.Contain(SurgeryOrganCondition.Nonfunctional));
        });
        await Server.WaitPost(() =>
        {
            var organs = Server.System<OrbitraOrganSystem>();
            foreach (var (kind, condition) in organs.GetConditions(patient))
            {
                if (condition == SurgeryOrganCondition.Healthy)
                    SComp<OrbitraOrganComponent>(organs.Installed(patient, kind)!.Value).Condition = SurgeryOrganCondition.Nonfunctional;
            }
        });
        await FireProjectile(patient, "BulletMagnum");
        await Server.WaitAssertion(() => Assert.That(Server.System<OrbitraOrganSystem>().GetConditions(patient).Values,
            Is.All.EqualTo(SurgeryOrganCondition.Nonfunctional)));
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
