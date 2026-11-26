#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Surgery;
using Content.Server.Lathe;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Body;
using Content.Shared.Buckle;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Lathe;
using Content.Shared.Movement.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra.Surgery;

public sealed class OrbitraProstheticsTest : GameTest
{
    private static readonly ProtoId<TechnologyPrototype> ProstheticsTech = "OrbitraMedicalProsthetics";
    private static readonly ProtoId<TagPrototype> InstantDoAfters = "InstantDoAfters";

    [Test]
    public async Task SurgeonFitsUsableHandOnChosenSideAndCanReturnOriginal()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var surgery = Server.System<OrbitraSurgerySystem>();
            var anatomy = Server.System<OrbitraSurgeryAnatomySystem>();
            var hands = Server.System<SharedHandsSystem>();
            var ui = Server.System<SharedUserInterfaceSystem>();
            var table = SSpawnAtPosition("OperatingTable", map.GridCoords);
            var patient = SSpawnAtPosition("MobHuman", map.GridCoords);
            var doctor = SSpawnAtPosition("MobHuman", map.GridCoords);
            var instrument = SSpawnAtPosition("Scalpel", map.GridCoords);
            var kit = SSpawnAtPosition("OrbitraProstheticArm", map.GridCoords);
            var drill = SSpawnAtPosition("Drill", map.GridCoords);
            const SurgeryZone zone = SurgeryZone.LeftArm;
            var originalHand = anatomy.FindOrgan(patient, "HandLeft");
            var torso = anatomy.FindOrgan(patient, "Torso")!.Value;
            var skin = SComp<VisualOrganComponent>(torso).Data.Color;
            var markings = SComp<VisualOrganMarkingsComponent>(torso).Markings;
            var originalMarkings = markings.SelectMany(pair => pair.Value).ToArray();

            Assert.That(surgery.TryInstallProsthesis(patient, zone, kit), Is.False);
            Assert.That(surgery.TryDetachLimb(patient, zone, out var original), Is.True);
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(patient, patient, table), Is.True);
            Assert.That(ui.TryOpenUi(table, OrbitraSurgeryUiKey.Key, doctor), Is.True);
            Server.System<TagSystem>().AddTag(doctor, InstantDoAfters);
            Assert.That(hands.TryPickup(doctor, drill), Is.True);
            Assert.That(surgery.TryStartStep(table, doctor, patient, zone, SurgeryProcedure.InstallProsthesis, 0, false), Is.True);
            Assert.That(hands.TryDrop(doctor, drill), Is.True);
            var wrong = SSpawnAtPosition("OrbitraProstheticLeg", map.GridCoords);
            Assert.That(hands.TryPickup(doctor, wrong), Is.True);
            Assert.That(surgery.TryStartStep(table, doctor, patient, zone, SurgeryProcedure.InstallProsthesis, 1, false), Is.False);
            Assert.That(hands.TryDrop(doctor, wrong), Is.True);
            Assert.That(hands.TryPickup(doctor, kit), Is.True);
            Assert.That(surgery.TryStartStep(table, doctor, patient, zone, SurgeryProcedure.InstallProsthesis, 1, false), Is.True);
            Assert.That(SEntMan.IsQueuedForDeletion(kit), Is.True);
            Assert.That(ui.TryGetUiState<OrbitraSurgeryState>(table, OrbitraSurgeryUiKey.Key, out var state), Is.True);
            Assert.That(state!.AcceptsProstheses, Is.True);
            Assert.That(state.Prostheses[zone], Is.False);
            var hand = anatomy.FindOrgan(patient, "HandLeft")!.Value;
            var arm = anatomy.FindOrgan(patient, "ArmLeft")!.Value;
            Assert.That(SComp<ChildOrganComponent>(hand).Parent, Is.EqualTo(arm));
            Assert.That(SComp<VisualOrganComponent>(arm).Data.Color, Is.EqualTo(SComp<OrbitraProsthesisComponent>(kit).Finish));
            Assert.That(SComp<VisualOrganComponent>(torso).Data.Color, Is.EqualTo(skin));
            Assert.That(SComp<VisualOrganMarkingsComponent>(torso).Markings.SelectMany(pair => pair.Value), Is.EqualTo(originalMarkings));
            Assert.That(hands.TryPickup(patient, instrument, "left"), Is.True);
            Assert.That(surgery.TryReattachLimb(patient, zone, original!.Value), Is.False);

            float Delay(string side)
            {
                hands.SetActiveHand(patient, side);
                var ev = new OrbitraHandActionDelayEvent(1f);
                SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
                return ev.Multiplier;
            }

            Assert.That(Delay("left"), Is.EqualTo(1.15f));
            Assert.That(Delay("right"), Is.EqualTo(1f));
            Assert.That(surgery.TryRemoveProsthesis(patient, zone, out var removed), Is.True);
            Assert.That(hands.IsHolding(patient, instrument), Is.False);
            Assert.That(hands.TryGetHand(patient, "left", out _), Is.False);
            Assert.That(anatomy.IsMissing(patient, zone), Is.True);
            Assert.That(SComp<MetaDataComponent>(removed!.Value).EntityPrototype?.ID, Is.EqualTo("OrbitraProstheticArm"));
            Assert.That(surgery.TryRemoveProsthesis(patient, zone, out _), Is.False);
            Assert.That(surgery.TryReattachLimb(patient, zone, original.Value), Is.True);
            Assert.That(anatomy.FindOrgan(patient, "HandLeft"), Is.EqualTo(originalHand));

            Assert.That(surgery.TryDetachLimb(patient, SurgeryZone.RightArm, out _), Is.True);
            Assert.That(surgery.TryInstallProsthesis(patient, SurgeryZone.RightArm, kit), Is.False);
            Assert.That(surgery.TryInstallProsthesis(patient, SurgeryZone.RightArm, removed.Value), Is.True);
            Assert.That(Delay("right"), Is.EqualTo(1.15f));
            var secondDoctor = SSpawnAtPosition("MobHuman", map.GridCoords);
            var retractor = SSpawnAtPosition("Retractor", map.GridCoords);
            Server.System<TagSystem>().AddTag(secondDoctor, InstantDoAfters);
            Assert.That(ui.TryOpenUi(table, OrbitraSurgeryUiKey.Key, secondDoctor), Is.True);
            Assert.That(hands.TryPickup(secondDoctor, retractor), Is.True);
            Assert.That(surgery.TryStartStep(table, secondDoctor, patient, SurgeryZone.RightArm, SurgeryProcedure.RemoveProsthesis, 0, false), Is.True);
            Assert.That(hands.TryDrop(secondDoctor, retractor), Is.True);
            Assert.That(hands.TryPickup(secondDoctor, drill), Is.True);
            Assert.That(surgery.TryStartStep(table, secondDoctor, patient, SurgeryZone.RightArm, SurgeryProcedure.RemoveProsthesis, 1, false), Is.True);
            Assert.That(anatomy.IsMissing(patient, SurgeryZone.RightArm), Is.True);
            var advanced = SSpawnAtPosition("OrbitraAdvancedProstheticArm", map.GridCoords);
            Assert.That(surgery.TryInstallProsthesis(patient, SurgeryZone.RightArm, advanced), Is.True);
            Assert.That(Delay("right"), Is.EqualTo(1f));
        });
    }

    [Test]
    public async Task ReptiliansRetainLimbReattachmentWithoutHumanProsthetics()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            const SurgeryZone zone = SurgeryZone.RightArm;
            var surgery = Server.System<OrbitraSurgerySystem>();
            var ui = Server.System<SharedUserInterfaceSystem>();
            var patient = SSpawnAtPosition("MobReptilian", map.GridCoords);
            var doctor = SSpawnAtPosition("MobHuman", map.GridCoords);
            var table = SSpawnAtPosition("OperatingTable", map.GridCoords);
            var kit = SSpawnAtPosition("OrbitraProstheticArm", map.GridCoords);
            var drill = SSpawnAtPosition("Drill", map.GridCoords);

            Assert.That(surgery.TryDetachLimb(patient, zone, out var original), Is.True);
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(patient, patient, table), Is.True);
            Assert.That(ui.TryOpenUi(table, OrbitraSurgeryUiKey.Key, doctor), Is.True);
            Assert.That(ui.TryGetUiState<OrbitraSurgeryState>(table, OrbitraSurgeryUiKey.Key, out var state), Is.True);
            Assert.That(state!.Injuries[zone], Is.EqualTo(SurgeryInjury.Missing));
            Assert.That(state.AcceptsProstheses, Is.False);
            Assert.That(Server.System<SharedHandsSystem>().TryPickup(doctor, drill), Is.True);
            Assert.That(surgery.TryStartStep(table, doctor, patient, zone, SurgeryProcedure.InstallProsthesis, 0, false), Is.False);
            Assert.That(surgery.TryInstallProsthesis(patient, zone, kit), Is.False);
            Assert.That(surgery.TryReattachLimb(patient, zone, original!.Value), Is.True);
        });
    }

    [Test]
    public async Task FittedFeetRestoreTierSpecificWalkAndRunSpeed()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var surgery = Server.System<OrbitraSurgerySystem>();
            var anatomy = Server.System<OrbitraSurgeryAnatomySystem>();
            var patient = SSpawnAtPosition("MobHuman", map.GridCoords);
            var speed = SComp<MovementSpeedModifierComponent>(patient);
            var originalWalk = speed.WalkSpeedModifier;
            var originalRun = speed.SprintSpeedModifier;

            foreach (var zone in new[] { SurgeryZone.RightLeg, SurgeryZone.LeftLeg })
            {
                var kit = SSpawnAtPosition("OrbitraProstheticLeg", map.GridCoords);
                Assert.That(surgery.TryDetachLimb(patient, zone, out _), Is.True);
                Assert.That(surgery.TryInstallProsthesis(patient, zone, kit), Is.True);
                var foot = anatomy.FindOrgan(patient, OrbitraSurgeryAnatomySystem.Extremity(zone)!)!.Value;
                Assert.That(SComp<ChildOrganComponent>(foot).Parent, Is.EqualTo(anatomy.FindOrgan(patient, OrbitraSurgeryAnatomySystem.Category(zone)!)));
                Assert.That(speed.WalkSpeedModifier, Is.EqualTo(originalWalk * 0.9f).Within(0.001f));
                Assert.That(speed.SprintSpeedModifier, Is.EqualTo(originalRun * 0.9f).Within(0.001f));
            }

            foreach (var zone in new[] { SurgeryZone.RightLeg, SurgeryZone.LeftLeg })
            {
                var kit = SSpawnAtPosition("OrbitraAdvancedProstheticLeg", map.GridCoords);
                Assert.That(surgery.TryInstallProsthesis(patient, zone, kit), Is.False);
                Assert.That(surgery.TryRemoveProsthesis(patient, zone, out _), Is.True);
                Assert.That(surgery.TryInstallProsthesis(patient, zone, kit), Is.True);
            }
            Assert.That(speed.WalkSpeedModifier, Is.EqualTo(originalWalk));
            Assert.That(speed.SprintSpeedModifier, Is.EqualTo(originalRun));
        });
    }

    [Test]
    public async Task StarterSuppliesAndResearchDistributeTheCorrectKits()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var containers = Server.System<SharedContainerSystem>();
            foreach (var (supply, container) in new[]
            {
                ("ClothingBackpackDuffelSurgeryFilled", "storagebase"),
                ("CrateMedicalSurgery", "entity_storage"),
            })
            {
                var items = containers.GetContainer(SSpawnAtPosition(supply, map.GridCoords), container).ContainedEntities;
                var kits = items.Where(SEntMan.HasComponent<OrbitraProsthesisComponent>)
                    .Select(item => SComp<MetaDataComponent>(item).EntityPrototype!.ID);
                Assert.That(kits, Is.EquivalentTo(new[] { "OrbitraProstheticArm", "OrbitraProstheticLeg" }), supply);
            }

            var tech = SProtoMan.Index(ProstheticsTech);
            var fabricator = SSpawnAtPosition("ExosuitFabricator", map.GridCoords);
            var lathe = Server.System<LatheSystem>();
            var machine = SComp<LatheComponent>(fabricator);
            var research = Server.System<ResearchSystem>();
            var database = SSpawnAtPosition("ResearchAndDevelopmentServer", map.GridCoords);
            var console = SSpawnAtPosition("ComputerResearchAndDevelopment", map.GridCoords);
            var researcher = SSpawnAtPosition("MobHuman", map.GridCoords);
            var power = Server.System<PowerReceiverSystem>();
            power.SetNeedsPower(database, false);
            power.SetNeedsPower(console, false);
            research.RegisterClient(console, database);
            research.RegisterClient(fabricator, database);
            research.ModifyServerPoints(database, tech.Cost);
            Assert.That(research.CanServerUnlockTechnology(console, tech), Is.False);
            research.AddTechnology(database, "MechanizedTreatment");
            ProtoId<LatheRecipePrototype>[] recipes = ["OrbitraAdvancedProstheticArm", "OrbitraAdvancedProstheticLeg"];
            foreach (var recipe in recipes)
            {
                Assert.That(tech.RecipeUnlocks, Does.Contain(recipe));
                Assert.That(lathe.GetAvailableRecipes(fabricator, machine), Does.Not.Contain(recipe));
                Assert.That(lathe.GetAvailableRecipes(fabricator, machine, getUnavailable: true), Does.Contain(recipe));
                Assert.That(SProtoMan.Index(recipe).Result?.Id, Is.EqualTo(recipe.Id));
            }
            Assert.That(research.UnlockTechnology(console, tech, researcher), Is.True);
            research.SyncClientWithServer(fabricator);
            foreach (var recipe in recipes)
                Assert.That(lathe.GetAvailableRecipes(fabricator, machine), Does.Contain(recipe));
        });
    }
}
