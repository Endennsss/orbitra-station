#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Surgery;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraSurgeryTableTest : GameTest
{
    private static readonly ProtoId<TagPrototype> InstantDoAfters = "InstantDoAfters";

    [Test]
    public async Task ReattachesOnlyOriginalOrgansToTheirOwnerAndSide()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var surgery = Server.System<OrbitraSurgerySystem>();
            var anatomy = Server.System<OrbitraSurgeryAnatomySystem>();
            var patient = SSpawnAtPosition("MobHuman", map.GridCoords);
            var other = SSpawnAtPosition("MobHuman", map.GridCoords);
            var leftArm = anatomy.FindOrgan(patient, "ArmLeft");
            var leftHand = anatomy.FindOrgan(patient, "HandLeft");
            var torso = anatomy.FindOrgan(patient, "Torso");
            var held = SSpawnAtPosition("Scalpel", map.GridCoords);
            var hands = Server.System<SharedHandsSystem>();
            Assert.That(hands.TryPickup(patient, held, "left"), Is.True);

            var blood = Server.System<BloodstreamSystem>();
            var bloodBefore = blood.GetBloodLevel(patient);
            var bleedRate = SComp<BloodstreamComponent>(patient).BleedAmount;
            Assert.That(surgery.TryDetachLimb(patient, SurgeryZone.LeftArm, out var limb), Is.True);
            Assert.That(blood.GetBloodLevel(patient), Is.LessThan(bloodBefore));
            Assert.That(SComp<BloodstreamComponent>(patient).BleedAmount, Is.GreaterThan(bleedRate));
            Assert.That(surgery.TryDetachLimb(other, SurgeryZone.LeftArm, out var foreign), Is.True);
            Assert.That(surgery.TryDetachLimb(patient, SurgeryZone.RightArm, out var opposite), Is.True);
            Assert.That(surgery.TryDetachLimb(patient, SurgeryZone.LeftArm, out _), Is.False);
            Assert.That(SComp<HandsComponent>(patient).Count, Is.Zero);
            Assert.That(hands.IsHolding(patient, held), Is.False);
            Assert.That(anatomy.FindOrgan(limb!.Value, "HandLeft"), Is.EqualTo(leftHand));

            Assert.That(surgery.TryReattachLimb(patient, SurgeryZone.RightArm, limb.Value), Is.False);
            Assert.That(surgery.TryReattachLimb(patient, SurgeryZone.LeftArm, foreign!.Value), Is.False);
            Assert.That(surgery.TryReattachLimb(patient, SurgeryZone.LeftArm, limb.Value), Is.True);
            Assert.That(surgery.TryReattachLimb(patient, SurgeryZone.LeftArm, limb.Value), Is.False);
            Assert.That(surgery.TryReattachLimb(patient, SurgeryZone.RightArm, opposite!.Value), Is.True);
            Assert.That(surgery.TryReattachLimb(other, SurgeryZone.LeftArm, foreign.Value), Is.True);
            Assert.That(anatomy.FindOrgan(patient, "ArmLeft"), Is.EqualTo(leftArm));
            Assert.That(anatomy.FindOrgan(patient, "HandLeft"), Is.EqualTo(leftHand));
            Assert.That(SComp<ChildOrganComponent>(leftArm!.Value).Parent, Is.EqualTo(torso));
            Assert.That(SComp<ChildOrganComponent>(leftHand!.Value).Parent, Is.EqualTo(leftArm));
            Assert.That(SComp<HandsComponent>(patient).Count, Is.EqualTo(2));
            Assert.That(hands.TryPickup(patient, held, "left"), Is.True);

            var speed = SComp<MovementSpeedModifierComponent>(patient);
            var initial = speed.WalkSpeedModifier;
            var foot = anatomy.FindOrgan(patient, "FootRight");
            Assert.That(surgery.TryDetachLimb(patient, SurgeryZone.RightLeg, out var leg), Is.True);
            Assert.That(speed.WalkSpeedModifier, Is.LessThan(initial));
            Assert.That(anatomy.FindOrgan(leg!.Value, "FootRight"), Is.EqualTo(foot));
            Assert.That(surgery.TryReattachLimb(patient, SurgeryZone.RightLeg, leg.Value), Is.True);
            Assert.That(speed.WalkSpeedModifier, Is.EqualTo(initial));
            Assert.That(anatomy.FindOrgan(patient, "FootRight"), Is.EqualTo(foot));
        });
    }

    [Test]
    public async Task NativeOrganRemovalClearsOnlyItsTraumaAndCancelsTheObsoleteStep()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var table = SSpawnAtPosition("OperatingTable", map.GridCoords);
            var patient = SSpawnAtPosition("MobHuman", map.GridCoords);
            var doctor = SSpawnAtPosition("MobHuman", map.GridCoords);
            var retractor = SSpawnAtPosition("Retractor", map.GridCoords);
            var setter = SSpawnAtPosition("OrbitraBoneSetter", map.GridCoords);
            var surgery = Server.System<OrbitraSurgerySystem>();
            var trauma = Server.System<OrbitraTraumaSystem>();
            var anatomy = Server.System<OrbitraSurgeryAnatomySystem>();
            var hands = Server.System<SharedHandsSystem>();
            var tags = Server.System<TagSystem>();
            const SurgeryZone zone = SurgeryZone.LeftLeg;
            var injuries = SEntMan.EnsureComponent<OrbitraTraumaComponent>(patient);
            injuries.Injuries[zone] = SurgeryInjury.Fracture | SurgeryInjury.Shrapnel;
            injuries.Injuries[SurgeryZone.Torso] = SurgeryInjury.InternalBleeding;
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(patient, patient, table), Is.True);
            Assert.That(Server.System<SharedUserInterfaceSystem>().TryOpenUi(table, OrbitraSurgeryUiKey.Key, doctor), Is.True);
            Assert.That(hands.TryPickup(doctor, retractor), Is.True);
            tags.AddTag(doctor, InstantDoAfters);
            SEntMan.EnsureComponent<OrbitraSurgicalAccessComponent>(patient).Zones[zone] = SurgeryAccess.Open;
            Assert.That(surgery.TryStartStep(table, doctor, patient, zone, SurgeryProcedure.Fracture, 0, false), Is.True);
            tags.RemoveTag(doctor, InstantDoAfters);
            Assert.That(hands.TryDrop(doctor, retractor), Is.True);
            Assert.That(hands.TryPickup(doctor, setter), Is.True);
            Assert.That(surgery.TryStartStep(table, doctor, patient, zone, SurgeryProcedure.Fracture, 1, false), Is.True);
            var pending = SComp<OrbitraSurgeryOperationComponent>(patient).Pending;

            var changes = 0;
            void Changed(EntityUid entity)
            {
                if (entity == patient)
                    changes++;
            }

            trauma.Changed += Changed;
            try
            {
                var leg = anatomy.FindOrgan(patient, "LegLeft")!.Value;
                Assert.That(Server.System<DetachableOrganSystem>().Detach(leg), Is.Not.Null);
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(injuries.Injuries.ContainsKey(zone), Is.False);
                Assert.That(trauma.GetInjuries(patient, zone), Is.EqualTo(SurgeryInjury.Missing));
                Assert.That(trauma.GetInjuries(patient, SurgeryZone.Torso), Is.EqualTo(SurgeryInjury.InternalBleeding));
                Assert.That(SEntMan.HasComponent<OrbitraSurgeryOperationComponent>(patient), Is.False);
                Assert.That(Server.System<SharedDoAfterSystem>().GetStatus(pending), Is.EqualTo(DoAfterStatus.Cancelled));

                Assert.That(hands.TryDrop(doctor, setter), Is.True);
                Assert.That(hands.TryPickup(doctor, SSpawnAtPosition("Hemostat", map.GridCoords)), Is.True);
                tags.AddTag(doctor, InstantDoAfters);
                Assert.That(surgery.TryStartStep(table, doctor, patient, zone, SurgeryProcedure.Reattach, 0, false), Is.True);
                Assert.That(SComp<OrbitraSurgeryOperationComponent>(patient).Step, Is.EqualTo(1));
                var containers = Server.System<SharedContainerSystem>();
                Assert.That(containers.Insert(leg, containers.GetContainer(patient, BodyComponent.ContainerID)), Is.True);
                Assert.That(SEntMan.HasComponent<OrbitraSurgeryOperationComponent>(patient), Is.False);

                Assert.That(surgery.TryDetachLimb(patient, SurgeryZone.RightArm, out _), Is.True);
                Assert.That(changes, Is.EqualTo(2));
            }
            finally
            {
                trauma.Changed -= Changed;
            }
        });
    }

    [Test]
    public async Task InterruptedFinalStepStaysWithPatientAndRejectsConcurrentSurgeons()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var surgery = Server.System<OrbitraSurgerySystem>();
            var anatomy = Server.System<OrbitraSurgeryAnatomySystem>();
            var buckle = Server.System<SharedBuckleSystem>();
            var hands = Server.System<SharedHandsSystem>();
            var tags = Server.System<TagSystem>();
            var ui = Server.System<SharedUserInterfaceSystem>();
            var table = SSpawnAtPosition("OperatingTable", map.GridCoords);
            var patient = SSpawnAtPosition("MobHuman", map.GridCoords);
            var surgeon = SSpawnAtPosition("MobHuman", map.GridCoords);
            var second = SSpawnAtPosition("MobHuman", map.GridCoords);
            var hemostat = SSpawnAtPosition("Hemostat", map.GridCoords);
            const SurgeryZone zone = SurgeryZone.LeftArm;
            const SurgeryProcedure procedure = SurgeryProcedure.Reattach;

            Assert.That(surgery.TryDetachLimb(patient, zone, out var limb), Is.True);
            Assert.That(buckle.TryBuckle(patient, patient, table), Is.True);
            Assert.That(ui.TryOpenUi(table, OrbitraSurgeryUiKey.Key, surgeon), Is.True);
            Assert.That(ui.TryOpenUi(table, OrbitraSurgeryUiKey.Key, second), Is.True);
            Assert.That(hands.TryPickup(surgeon, hemostat), Is.True);
            tags.AddTag(surgeon, InstantDoAfters);
            Assert.That(surgery.TryStartStep(table, surgeon, patient, zone, procedure, 0, false), Is.True);
            tags.RemoveTag(surgeon, InstantDoAfters);
            Assert.That(hands.TryDrop(surgeon, hemostat), Is.True);
            Assert.That(hands.TryPickup(surgeon, limb!.Value), Is.True);
            Assert.That(surgery.TryStartStep(table, surgeon, patient, zone, procedure, 1, false), Is.True);
            Assert.That(surgery.TryStartStep(table, second, patient, zone, procedure, 1, false), Is.False);
            Assert.That(surgery.TryAbandon(table, second, patient, zone, procedure, 1), Is.False);
            buckle.Unbuckle(patient, surgeon);

            Assert.That(anatomy.IsMissing(patient, zone), Is.True);
            Assert.That(SComp<OrbitraSurgeryOperationComponent>(patient).Step, Is.EqualTo(1));
            Assert.That(SComp<OrbitraSurgeryOperationComponent>(patient).Surgeon, Is.Null);
            Assert.That(hands.GetActiveItem(surgeon), Is.EqualTo(limb));
            var nextTable = SSpawnAtPosition("OperatingTable", map.GridCoords);
            Assert.That(buckle.TryBuckle(patient, patient, nextTable), Is.True);
            Assert.That(ui.TryOpenUi(nextTable, OrbitraSurgeryUiKey.Key, second), Is.True);
            Assert.That(hands.TryDrop(surgeon, limb.Value), Is.True);
            Assert.That(hands.TryPickup(second, limb.Value), Is.True);
            tags.AddTag(second, InstantDoAfters);
            Assert.That(surgery.TryStartStep(table, second, patient, zone, procedure, 1, false), Is.False);
            Assert.That(surgery.TryStartStep(nextTable, second, patient, zone, procedure, 1, false), Is.True);
            Assert.That(surgery.TryStartStep(nextTable, second, patient, zone, procedure, 1, false), Is.False);
            Assert.That(anatomy.IsMissing(patient, zone), Is.False);
            Assert.That(SEntMan.HasComponent<OrbitraSurgeryOperationComponent>(patient), Is.False);
            Assert.That(SComp<HandsComponent>(patient).Count, Is.EqualTo(2));
        });
    }
}
