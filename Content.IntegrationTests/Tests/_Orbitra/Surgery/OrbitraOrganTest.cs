#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Surgery;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Server.Lathe;
using Content.Server.Medical.HealthAnalyzer;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra.Surgery;

public sealed class OrbitraOrganTest : GameTest
{
    private static readonly ProtoId<TagPrototype> InstantDoAfters = "InstantDoAfters";
    private static readonly ProtoId<TechnologyPrototype> CultureTechnology = "OrbitraOrganCulture";

    [TestPrototypes]
    private const string TestPrototypes = """
        - type: orbitraTrauma
          id: OrbitraTestOrganTrauma
          fracture: { threshold: 100, chance: 0 }
          internalBleeding: { threshold: 100, chance: 0 }
          shrapnel: { threshold: 100, chance: 0 }
          dismemberment: { threshold: 100, chance: 0 }
          organInjury: { threshold: 10, chance: 1 }
          organFailure: { threshold: 16, chance: 1 }
          fragmentProjectiles: []

        - type: damageModifierSet
          id: OrbitraTestOrganArmor
          coefficients: { Blunt: 0.5 }

        - type: entity
          id: OrbitraTestOrganPatient
          parent: MobHuman
          components:
          - type: OrbitraSurgicalPatient
            traumaProfile: OrbitraTestOrganTrauma
          - type: Damageable
            damageModifierSet: OrbitraTestOrganArmor
        """;

    [Test]
    public async Task OnePostResistanceHitAdvancesASingleOrganByAtMostOneStage()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var patient = SSpawnAtPosition("OrbitraTestOrganPatient", map.GridCoords);
            var organs = Server.System<OrbitraOrganSystem>();
            var damage = Server.System<DamageableSystem>();
            void Hit(int amount) => damage.TryChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Blunt"] = amount } });
            for (var i = 0; i < 5; i++)
                Hit(8);
            damage.TryChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Heat"] = 10 } });
            Hit(19);
            Assert.That(organs.GetConditions(patient).Values, Is.All.EqualTo(SurgeryOrganCondition.Healthy));

            Hit(32);
            var first = organs.GetConditions(patient);
            var kind = first.Single(x => x.Value == SurgeryOrganCondition.Damaged).Key;
            Assert.That(first.Values.Count(x => x == SurgeryOrganCondition.Healthy), Is.EqualTo(2));
            Hit(32);
            var second = organs.GetConditions(patient);
            Assert.That(second[kind], Is.EqualTo(SurgeryOrganCondition.Nonfunctional));
            Assert.That(second.Values.Count(x => x == SurgeryOrganCondition.Healthy), Is.EqualTo(2));

            var scan = Server.System<ServerHealthAnalyzerSystem>().GetHealthAnalyzerUiState(patient, false);
            Assert.That(scan.OrbitraOrgans, Is.EquivalentTo(second));
        });
    }

    [TestCase(SurgeryOrgan.Heart)]
    [TestCase(SurgeryOrgan.Lungs)]
    [TestCase(SurgeryOrgan.Liver)]
    public async Task SurgeryMovesTheSameOrganOnlyOnCompletion(SurgeryOrgan kind)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var organs = Server.System<OrbitraOrganSystem>();
            var surgery = Server.System<OrbitraSurgerySystem>();
            var hands = Server.System<SharedHandsSystem>();
            var tags = Server.System<TagSystem>();
            var ui = Server.System<SharedUserInterfaceSystem>();
            var table = SSpawnAtPosition("OperatingTable", map.GridCoords);
            var doctor = SSpawnAtPosition("MobHuman", map.GridCoords);
            var patient = SSpawnAtPosition("MobHuman", map.GridCoords);
            var original = organs.Installed(patient, kind)!.Value;
            var torso = Server.System<OrbitraSurgeryAnatomySystem>().FindOrgan(patient, "Torso")!.Value;
            var donorPatient = SSpawnAtPosition("MobHuman", map.GridCoords);
            var donor = organs.Installed(donorPatient, kind)!.Value;
            var buckle = Server.System<SharedBuckleSystem>();
            SComp<OrbitraOrganComponent>(donor).Condition = SurgeryOrganCondition.Damaged;
            Assert.That(buckle.TryBuckle(donorPatient, doctor, table), Is.True);
            Assert.That(ui.TryOpenUi(table, OrbitraSurgeryUiKey.Key, doctor), Is.True);
            tags.AddTag(doctor, InstantDoAfters);
            CompleteProcedure(table, doctor, donorPatient, kind, SurgeryProcedure.ExtractOrgan);
            Assert.That(organs.Installed(donorPatient, kind), Is.Null);
            Assert.That(organs.IsDonor(donor, kind), Is.True);
            buckle.Unbuckle(donorPatient, doctor);
            Assert.That(buckle.TryBuckle(patient, doctor, table), Is.True);
            bool Step(SurgeryProcedure procedure, int step, bool confirmed = false) =>
                surgery.TryStartStep(table, doctor, patient, SurgeryZone.Torso, procedure, step, confirmed, kind);
            void Tool(string id) => Hold(doctor, SSpawnAtPosition(id, map.GridCoords));

            Tool("Scalpel");
            Assert.That(Step(SurgeryProcedure.RepairOrgan, 0), Is.False);
            Assert.That(Step(SurgeryProcedure.ExtractOrgan, 0), Is.False);
            Assert.That(Step(SurgeryProcedure.ExtractOrgan, 0, true), Is.True);
            Tool("Retractor");
            var other = kind == SurgeryOrgan.Heart ? SurgeryOrgan.Liver : SurgeryOrgan.Heart;
            Assert.That(surgery.TryStartStep(table, doctor, patient, SurgeryZone.Torso, SurgeryProcedure.ExtractOrgan, 1, false, other), Is.False);
            Assert.That(Step(SurgeryProcedure.ExtractOrgan, 1), Is.True);

            Tool("Hemostat");
            tags.RemoveTag(doctor, InstantDoAfters);
            Assert.That(Step(SurgeryProcedure.ExtractOrgan, 2), Is.True);
            ui.CloseUi(table, OrbitraSurgeryUiKey.Key, doctor);
            Assert.That(organs.Installed(patient, kind), Is.EqualTo(original));
            Assert.That(SComp<ChildOrganComponent>(original).Parent, Is.EqualTo(torso));
            Assert.That(ui.TryOpenUi(table, OrbitraSurgeryUiKey.Key, doctor), Is.True);
            tags.AddTag(doctor, InstantDoAfters);
            Assert.That(Step(SurgeryProcedure.ExtractOrgan, 2), Is.True);
            Assert.That(Step(SurgeryProcedure.ExtractOrgan, 2), Is.False);
            Assert.That(organs.Condition(patient, kind), Is.EqualTo(SurgeryOrganCondition.Missing));
            Assert.That(SComp<OrganComponent>(original).Body, Is.Null);
            Assert.That(SComp<ChildOrganComponent>(original).Parent, Is.Null);
            Assert.That(Server.System<OrganRelationSystem>().HasDirectChild((torso, SComp<ParentOrganComponent>(torso)), original), Is.False);
            Assert.That(organs.IsDonor(original, kind), Is.True);

            Tool("Retractor");
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 0), Is.True);
            Tool("Hemostat");
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 1), Is.True);
            Tool("OrganAnimal" + kind);
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 2), Is.False);
            Tool(OrbitraOrganSystem.Prototype(other));
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 2), Is.False);
            Hold(doctor, donor);
            SComp<OrbitraOrganComponent>(donor).Condition = SurgeryOrganCondition.Nonfunctional;
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 2), Is.False);
            SComp<OrbitraOrganComponent>(donor).Condition = SurgeryOrganCondition.Damaged;
            tags.RemoveTag(doctor, InstantDoAfters);
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 2), Is.True);
            var secondDoctor = SSpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(ui.TryOpenUi(table, OrbitraSurgeryUiKey.Key, secondDoctor), Is.True);
            Hold(secondDoctor, original);
            Assert.That(surgery.TryStartStep(table, secondDoctor, patient, SurgeryZone.Torso,
                SurgeryProcedure.TransplantOrgan, 2, false, kind), Is.False);
            Assert.That(hands.GetActiveItem(secondDoctor), Is.EqualTo(original));
            ui.CloseUi(table, OrbitraSurgeryUiKey.Key, doctor);
            Assert.That(hands.GetActiveItem(doctor), Is.EqualTo(donor));
            Assert.That(organs.Installed(patient, kind), Is.Null);
            Assert.That(ui.TryOpenUi(table, OrbitraSurgeryUiKey.Key, doctor), Is.True);
            tags.AddTag(doctor, InstantDoAfters);
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 2), Is.True);
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 2), Is.False);
            Assert.That(organs.Installed(patient, kind), Is.EqualTo(donor));
            Assert.That(SComp<OrganComponent>(donor).Body, Is.EqualTo(patient));
            Assert.That(SComp<ChildOrganComponent>(donor).Parent, Is.EqualTo(torso));
            Assert.That(Server.System<OrganRelationSystem>().HasDirectChild((torso, SComp<ParentOrganComponent>(torso)), donor), Is.True);
            Assert.That(organs.Condition(patient, kind), Is.EqualTo(SurgeryOrganCondition.Damaged));

            CompleteProcedure(table, doctor, patient, kind, SurgeryProcedure.RepairOrgan);
            Assert.That(organs.Condition(patient, kind), Is.EqualTo(SurgeryOrganCondition.Healthy));
            Assert.That(organs.Installed(patient, kind), Is.EqualTo(donor));
        });
    }

    [Test]
    public async Task OrganConditionsChangeBloodGasExchangeAndMetaboliteClearance()
    {
        var map = await Pair.CreateTestMap();
        var patients = new EntityUid[3];
        var solutions = new Solution[3];
        var bloodSolutions = new Solution[3];
        await Server.WaitAssertion(() =>
        {
            var organs = Server.System<OrbitraOrganSystem>();
            var chemistry = Server.System<SharedSolutionContainerSystem>();
            var blood = Server.System<BloodstreamSystem>();
            var air = new GasMixture(0.5f) { Temperature = Atmospherics.T20C };
            air.SetMoles(Gas.Oxygen, 0.00194f);
            var environment = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
            environment.SetMoles(Gas.Oxygen, Atmospherics.OxygenMolesStandard);
            environment.SetMoles(Gas.Nitrogen, Atmospherics.NitrogenMolesStandard);
            Server.System<AtmosphereSystem>().SetMapAtmosphere(map.MapUid, false, environment);
            var oxygen = new float[patients.Length];
            var states = new[] { SurgeryOrganCondition.Healthy, SurgeryOrganCondition.Damaged, SurgeryOrganCondition.Nonfunctional };
            for (var i = 0; i < states.Length; i++)
            {
                var uid = patients[i] = SSpawnAtPosition("MobHuman", new EntityCoordinates(map.MapUid, 5 + i, 5));
                foreach (var kind in OrbitraOrganSystem.Kinds)
                    SComp<OrbitraOrganComponent>(organs.Installed(uid, kind)!.Value).Condition = states[i];
                var breathing = Server.System<RespiratorSystem>();
                Assert.That(breathing.CanMetabolizeInhaledAir(uid, air), Is.EqualTo(i == 0));
                breathing.Inhale(uid);
                var lung = SComp<LungComponent>(organs.Installed(uid, SurgeryOrgan.Lungs)!.Value);
                oxygen[i] = lung.Air.GetMoles(Gas.Oxygen);
                Assert.That(blood.TryModifyBloodLevel(uid, -15), Is.True);
                Assert.That(chemistry.TryGetSolution(uid, BloodstreamComponent.DefaultBloodSolutionName, out _, out var bloodContents), Is.True);
                bloodSolutions[i] = bloodContents!;
                Assert.That(chemistry.TryGetSolution(uid, BloodstreamComponent.DefaultMetabolitesSolutionName, out var sol, out var contents), Is.True);
                Assert.That(chemistry.TryAddReagent(sol!.Value, "Water", 20), Is.True);
                solutions[i] = contents!;
            }
            Assert.That(oxygen[0], Is.GreaterThan(oxygen[1]));
            Assert.That(oxygen[1], Is.GreaterThan(0f));
            Assert.That(oxygen[2], Is.Zero);
        });
        await Pair.RunTicksSync((int) Math.Ceiling(6.2 * SGameTiming.TickRate));
        await Server.WaitAssertion(() =>
        {
            // Доля крови на мониторе округлена до процента. Здесь измеряем объём в самом растворе.
            var volumes = bloodSolutions.Select(s => s.GetTotalPrototypeQuantity("Blood").Float()).ToArray();
            Assert.That(volumes[0], Is.GreaterThan(volumes[1]));
            Assert.That(volumes[1], Is.GreaterThan(volumes[2]));
            var water = solutions.Select(s => s.GetTotalPrototypeQuantity("Water").Float()).ToArray();
            Assert.That(water[0], Is.LessThan(water[1]));
            Assert.That(water[1], Is.LessThan(water[2]));
            Assert.That(water[2], Is.EqualTo(20f));
        });
    }

    [Test]
    public async Task MedicalTechFabAcceptsBiomassAndUnlocksOnlyTheThreeCultures()
    {
        var map = await Pair.CreateTestMap();
        EntityUid fab = default;
        await Server.WaitPost(() =>
        {
            fab = SSpawnAtPosition("MedicalTechFab", map.GridCoords);
            Server.System<PowerReceiverSystem>().SetNeedsPower(fab, false);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var user = SSpawnAtPosition("MobHuman", map.GridCoords);
            var lathe = Server.System<LatheSystem>();
            var materials = Server.System<SharedMaterialStorageSystem>();
            var technology = SProtoMan.Index(CultureTechnology);
            Assert.That(technology.RecipeUnlocks.Count, Is.EqualTo(3));
            Assert.That(materials.TryInsertMaterialEntity(user, SSpawnAtPosition("MaterialBiomass", map.GridCoords), fab), Is.True);
            Assert.That(materials.TryInsertMaterialEntity(user, SSpawnAtPosition("MaterialBiomass", map.GridCoords), fab), Is.True);
            Assert.That(materials.GetMaterialAmount(fab, "Biomass"), Is.EqualTo(200));
            var available = lathe.GetAvailableRecipes(fab, SComp<LatheComponent>(fab));
            Assert.That(technology.RecipeUnlocks.Any(available.Contains), Is.False);
            Server.System<ResearchSystem>().AddTechnology(fab, technology);
            foreach (var id in technology.RecipeUnlocks)
            {
                var recipe = SProtoMan.Index(id);
                Assert.That(recipe.Materials.ContainsKey("Biomass"), Is.True);
                Assert.That(lathe.TryAddToQueue(fab, recipe, 1), Is.True);
            }
            Assert.That(lathe.TryStartProducing(fab), Is.True);
            foreach (var _ in technology.RecipeUnlocks)
                lathe.FinishProducing(fab);

            var outputs = new Dictionary<SurgeryOrgan, EntityUid>();
            var organs = Server.System<OrbitraOrganSystem>();
            var items = SEntMan.AllEntityQueryEnumerator<OrbitraOrganComponent, TransformComponent>();
            while (items.MoveNext(out var uid, out var item, out var transform))
            {
                if (transform.Coordinates != SComp<TransformComponent>(fab).Coordinates || !organs.IsDonor(uid, item.Kind))
                    continue;
                Assert.That(item.Condition, Is.EqualTo(SurgeryOrganCondition.Healthy));
                Assert.That(outputs.TryAdd(item.Kind, uid), Is.True);
            }
            Assert.That(outputs.Keys, Is.EquivalentTo(OrbitraOrganSystem.Kinds));

            var patient = SSpawnAtPosition("MobHuman", map.GridCoords);
            var table = SSpawnAtPosition("OperatingTable", map.GridCoords);
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(patient, user, table), Is.True);
            Assert.That(Server.System<SharedUserInterfaceSystem>().TryOpenUi(table, OrbitraSurgeryUiKey.Key, user), Is.True);
            Server.System<TagSystem>().AddTag(user, InstantDoAfters);
            foreach (var (kind, printed) in outputs)
            {
                CompleteProcedure(table, user, patient, kind, SurgeryProcedure.ExtractOrgan);
                CompleteProcedure(table, user, patient, kind, SurgeryProcedure.TransplantOrgan, printed);
                Assert.That(organs.Installed(patient, kind), Is.EqualTo(printed));
                Assert.That(SComp<OrganComponent>(printed).Body, Is.EqualTo(patient));
                Assert.That(organs.Condition(patient, kind), Is.EqualTo(SurgeryOrganCondition.Healthy));
            }
        });
    }

    private void Hold(EntityUid doctor, EntityUid item)
    {
        var hands = Server.System<SharedHandsSystem>();
        if (hands.GetActiveItem(doctor) is { } held)
            Assert.That(hands.TryDrop(doctor, held), Is.True);
        Assert.That(hands.TryPickup(doctor, item), Is.True);
    }

    private void CompleteProcedure(EntityUid table, EntityUid doctor, EntityUid patient, SurgeryOrgan kind,
        SurgeryProcedure procedure, EntityUid? donor = null)
    {
        var steps = OrbitraSurgerySteps.Get(procedure);
        for (var i = 0; i < steps.Length; i++)
        {
            var item = steps[i] == OrbitraSurgerySteps.DonorOrgan ? donor!.Value :
                SSpawnAtPosition(steps[i], SComp<TransformComponent>(doctor).Coordinates);
            Hold(doctor, item);
            Assert.That(Server.System<OrbitraSurgerySystem>().TryStartStep(table, doctor, patient,
                SurgeryZone.Torso, procedure, i, true, kind), Is.True, $"{procedure}/{kind}/{i}");
        }
    }
}
