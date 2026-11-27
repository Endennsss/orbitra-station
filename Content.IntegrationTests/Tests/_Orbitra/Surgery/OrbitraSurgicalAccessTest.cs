using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Surgery;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra.Surgery;

[TestFixture]
public sealed class OrbitraSurgicalAccessTest : GameTest
{
    private static readonly ProtoId<TagPrototype> Instant = "InstantDoAfters";

    [Test]
    public async Task IncisionTravelsWithPatientAndSecondDoctorCanContinue()
    {
        var map = await Pair.CreateTestMap();
        var surgery = Server.System<OrbitraSurgerySystem>();
        var hands = Server.System<SharedHandsSystem>();
        var buckle = Server.System<SharedBuckleSystem>();
        var tables = Server.System<SharedUserInterfaceSystem>();
        var tags = Server.System<TagSystem>();
        var blood = Server.System<BloodstreamSystem>();
        var coords = map.GridCoords;
        EntityUid patient = default, first = default, second = default, table = default;
        const SurgeryZone zone = SurgeryZone.Torso;
        float before = 0;
        void Hold(EntityUid doctor, string type)
        {
            if (hands.GetActiveItem(doctor) is { } held)
                Assert.That(hands.TryDrop(doctor, held), Is.True);
            Assert.That(hands.TryPickup(doctor, SSpawnAtPosition(type, coords)), Is.True);
        }

        await Server.WaitAssertion(() =>
        {
            table = SSpawnAtPosition("OperatingTable", coords);
            patient = SSpawnAtPosition("MobHuman", coords);
            first = SSpawnAtPosition("MobHuman", coords);
            second = SSpawnAtPosition("MobHuman", coords);
            Assert.That(buckle.TryBuckle(patient, first, table), Is.True);
            Assert.That(tables.TryOpenUi(table, OrbitraSurgeryUiKey.Key, first), Is.True);
            tags.AddTag(first, Instant);
            Hold(first, "Scalpel");
            Assert.That(surgery.TryStartStep(table, first, patient, zone, SurgeryProcedure.OpenAccess, 0, false), Is.True);
            before = blood.GetOrbitraMonitoredBloodLevel(patient)!.Value;
            Assert.That(SComp<OrbitraSurgicalAccessComponent>(patient).Zones[zone], Is.EqualTo(SurgeryAccess.Incised));
        });

        await Pair.RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(blood.GetOrbitraMonitoredBloodLevel(patient), Is.LessThan(before));
            Hold(first, "Gauze");
            var stack = SComp<StackComponent>(hands.GetActiveItem(first)!.Value);
            var count = stack.Count;
            Assert.That(surgery.TryStartStep(table, first, patient, zone, SurgeryProcedure.DressAccess, 0, false), Is.True);
            Assert.That(stack.Count, Is.EqualTo(count - 1));
            buckle.Unbuckle(patient, first);
            var next = SSpawnAtPosition("OperatingTable", coords);
            Assert.That(buckle.TryBuckle(patient, second, next), Is.True);
            Assert.That(tables.TryOpenUi(next, OrbitraSurgeryUiKey.Key, second), Is.True);
            Assert.That(SComp<OrbitraSurgicalAccessComponent>(patient).DressingRemaining.ContainsKey(zone), Is.True);
            Hold(second, "Retractor");
            Assert.That(surgery.TryStartStep(next, second, patient, zone, SurgeryProcedure.OpenAccess, 1, false), Is.False);
            Hold(second, "Hemostat");
            tags.AddTag(second, Instant);
            Assert.That(surgery.TryStartStep(next, second, patient, zone, SurgeryProcedure.OpenAccess, 1, false), Is.True);
            Assert.That(SComp<OrbitraSurgicalAccessComponent>(patient).DressingRemaining, Is.Empty);
            Hold(second, "Retractor");
            Assert.That(surgery.TryStartStep(next, second, patient, zone, SurgeryProcedure.OpenAccess, 2, false), Is.True);
            buckle.Unbuckle(patient, second);
            Hold(first, "Gauze");
            var gauze = hands.GetActiveItem(first)!.Value;
            var remaining = SComp<StackComponent>(gauze).Count;
            Assert.That(Server.System<SharedInteractionSystem>().InteractUsing(first, gauze, patient,
                SComp<TransformComponent>(patient).Coordinates), Is.True);
            Assert.That(SComp<StackComponent>(gauze).Count, Is.EqualTo(remaining - 1));
            Assert.That(SComp<OrbitraSurgicalAccessComponent>(patient).DressingRemaining.ContainsKey(zone), Is.True);
            Assert.That(buckle.TryBuckle(patient, second, next), Is.True);
            Assert.That(tables.IsUiOpen(next, OrbitraSurgeryUiKey.Key, second) ||
                tables.TryOpenUi(next, OrbitraSurgeryUiKey.Key, second), Is.True);
            Hold(second, "Cautery");
            Assert.That(surgery.TryStartStep(next, second, patient, zone, SurgeryProcedure.CloseAccess, 0, false), Is.True);
            Assert.That(SEntMan.HasComponent<OrbitraSurgicalAccessComponent>(patient), Is.False);

            Hold(second, "Scalpel");
            tags.RemoveTag(second, Instant);
            var doAfter = Server.System<SharedDoAfterSystem>();
            Assert.That(surgery.TryStartStep(next, second, patient, SurgeryZone.Head, SurgeryProcedure.OpenAccess, 0, false), Is.True);
            var awake = SComp<OrbitraSurgeryOperationComponent>(patient);
            var awakeDuration = awake.Duration.TotalSeconds;
            doAfter.Cancel(awake.Pending);
            Assert.That(SEntMan.HasComponent<OrbitraSurgicalAccessComponent>(patient), Is.False);
            SEntMan.EnsureComponent<SleepingComponent>(patient);
            Assert.That(surgery.TryStartStep(next, second, patient, SurgeryZone.Head, SurgeryProcedure.OpenAccess, 0, false), Is.True);
            var op = SComp<OrbitraSurgeryOperationComponent>(patient);
            Assert.That(op.Duration.TotalSeconds, Is.EqualTo(awakeDuration * 0.75).Within(.01));
            Assert.That(tables.TryOpenUi(next, OrbitraSurgeryUiKey.Key, first), Is.True);
            Hold(first, "Scalpel");
            Assert.That(surgery.TryStartStep(next, first, patient, SurgeryZone.Head, SurgeryProcedure.OpenAccess, 0, false), Is.False);
            doAfter.Cancel(op.Pending);
            Assert.That(SEntMan.HasComponent<OrbitraSurgicalAccessComponent>(patient), Is.False);
        });
    }
}
