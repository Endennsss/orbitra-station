#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Surgery;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Lathe;
using Content.Server.Medical.HealthAnalyzer;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Administration.Systems;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Shared.Radio.Components;
using Content.Shared.Tag;
using Robust.Client.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
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
            Assert.That(first.Values.Count(x => x == SurgeryOrganCondition.Healthy), Is.EqualTo(OrbitraOrganSystem.Kinds.Length - 1));
            Hit(32);
            var second = organs.GetConditions(patient);
            Assert.That(second[kind], Is.EqualTo(SurgeryOrganCondition.Nonfunctional));
            Assert.That(second.Values.Count(x => x == SurgeryOrganCondition.Healthy), Is.EqualTo(OrbitraOrganSystem.Kinds.Length - 1));

            var scan = Server.System<ServerHealthAnalyzerSystem>().GetHealthAnalyzerUiState(patient, false);
            Assert.That(scan.OrbitraOrgans, Is.EquivalentTo(second));
        });
    }

    [TestCase(SurgeryOrgan.Heart)]
    [TestCase(SurgeryOrgan.Lungs)]
    [TestCase(SurgeryOrgan.Liver)]
    [TestCase(SurgeryOrgan.Kidneys)]
    [TestCase(SurgeryOrgan.Stomach)]
    [TestCase(SurgeryOrgan.Eyes)]
    [TestCase(SurgeryOrgan.Ears)]
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
            var zone = OrbitraOrganSystem.ZoneFor(kind);
            var region = Server.System<OrbitraSurgeryAnatomySystem>().FindOrgan(patient, zone.ToString())!.Value;
            var donorPatient = SSpawnAtPosition("MobHuman", map.GridCoords);
            var donor = organs.Installed(donorPatient, kind)!.Value;
            var buckle = Server.System<SharedBuckleSystem>();
            SComp<OrbitraOrganComponent>(donor).Condition = SurgeryOrganCondition.Damaged;
            Assert.That(buckle.TryBuckle(donorPatient, doctor, table), Is.True);
            Assert.That(ui.TryOpenUi(table, OrbitraSurgeryUiKey.Key, doctor), Is.True);
            tags.AddTag(doctor, InstantDoAfters);
            CompleteProcedure(table, doctor, donorPatient, kind, SurgeryProcedure.OpenAccess);
            CompleteProcedure(table, doctor, donorPatient, kind, SurgeryProcedure.ExtractOrgan);
            Assert.That(organs.Installed(donorPatient, kind), Is.Null);
            Assert.That(organs.IsDonor(donor, kind), Is.True);
            buckle.Unbuckle(donorPatient, doctor);
            Assert.That(buckle.TryBuckle(patient, doctor, table), Is.True);
            bool Step(SurgeryProcedure procedure, int step, bool confirmed = false) =>
                surgery.TryStartStep(table, doctor, patient, zone, procedure, step, confirmed, kind);
            void Tool(string id) => Hold(doctor, SSpawnAtPosition(id, map.GridCoords));

            Tool("Retractor");
            Assert.That(Step(SurgeryProcedure.ExtractOrgan, 0, true), Is.False);
            CompleteProcedure(table, doctor, patient, kind, SurgeryProcedure.OpenAccess);
            Tool("Hemostat");
            Assert.That(Step(SurgeryProcedure.RepairOrgan, 0), Is.False);
            Tool("Retractor");
            var wrongZone = zone == SurgeryZone.Head ? SurgeryZone.Torso : SurgeryZone.Head;
            Assert.That(surgery.TryStartStep(table, doctor, patient, wrongZone, SurgeryProcedure.ExtractOrgan, 0, true, kind), Is.False);
            Assert.That(Step(SurgeryProcedure.ExtractOrgan, 0), Is.False);
            Assert.That(Step(SurgeryProcedure.ExtractOrgan, 0, true), Is.True);
            Tool("Hemostat");
            var other = kind == SurgeryOrgan.Heart ? SurgeryOrgan.Liver : SurgeryOrgan.Heart;
            Assert.That(surgery.TryStartStep(table, doctor, patient, zone, SurgeryProcedure.ExtractOrgan, 1, false, other), Is.False);
            tags.RemoveTag(doctor, InstantDoAfters);
            Assert.That(Step(SurgeryProcedure.ExtractOrgan, 1), Is.True);
            ui.CloseUi(table, OrbitraSurgeryUiKey.Key, doctor);
            Assert.That(organs.Installed(patient, kind), Is.EqualTo(original));
            Assert.That(SComp<ChildOrganComponent>(original).Parent, Is.EqualTo(region));
            Assert.That(ui.TryOpenUi(table, OrbitraSurgeryUiKey.Key, doctor), Is.True);
            tags.AddTag(doctor, InstantDoAfters);
            Assert.That(Step(SurgeryProcedure.ExtractOrgan, 1), Is.True);
            Assert.That(Step(SurgeryProcedure.ExtractOrgan, 1), Is.False);
            Assert.That(organs.Condition(patient, kind), Is.EqualTo(SurgeryOrganCondition.Missing));
            Assert.That(SComp<OrganComponent>(original).Body, Is.Null);
            Assert.That(SComp<ChildOrganComponent>(original).Parent, Is.Null);
            Assert.That(Server.System<OrganRelationSystem>().HasDirectChild((region, SComp<ParentOrganComponent>(region)), original), Is.False);
            Assert.That(organs.IsDonor(original, kind), Is.True);

            Tool("Hemostat");
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 0), Is.True);
            Tool("OrganReptilian" + kind);
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 1), Is.False);
            Tool(OrbitraOrganSystem.Prototype(other));
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 1), Is.False);
            Hold(doctor, donor);
            SComp<OrbitraOrganComponent>(donor).Condition = SurgeryOrganCondition.Nonfunctional;
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 1), Is.False);
            SComp<OrbitraOrganComponent>(donor).Condition = SurgeryOrganCondition.Damaged;
            tags.RemoveTag(doctor, InstantDoAfters);
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 1), Is.True);
            var secondDoctor = SSpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(ui.TryOpenUi(table, OrbitraSurgeryUiKey.Key, secondDoctor), Is.True);
            Hold(secondDoctor, original);
            Assert.That(surgery.TryStartStep(table, secondDoctor, patient, zone,
                SurgeryProcedure.TransplantOrgan, 1, false, kind), Is.False);
            Assert.That(hands.GetActiveItem(secondDoctor), Is.EqualTo(original));
            ui.CloseUi(table, OrbitraSurgeryUiKey.Key, doctor);
            Assert.That(hands.GetActiveItem(doctor), Is.EqualTo(donor));
            Assert.That(organs.Installed(patient, kind), Is.Null);
            Assert.That(ui.TryOpenUi(table, OrbitraSurgeryUiKey.Key, doctor), Is.True);
            tags.AddTag(doctor, InstantDoAfters);
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 1), Is.True);
            Assert.That(Step(SurgeryProcedure.TransplantOrgan, 1), Is.False);
            Assert.That(organs.Installed(patient, kind), Is.EqualTo(donor));
            Assert.That(SComp<OrganComponent>(donor).Body, Is.EqualTo(patient));
            Assert.That(SComp<ChildOrganComponent>(donor).Parent, Is.EqualTo(region));
            Assert.That(Server.System<OrganRelationSystem>().HasDirectChild((region, SComp<ParentOrganComponent>(region)), donor), Is.True);
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
    public async Task KidneysAndStomachSlowIndependentlyOfTheLiver()
    {
        var map = await Pair.CreateTestMap();
        var residuals = new List<Solution>();
        var meals = new List<Solution>();
        await Server.WaitAssertion(() =>
        {
            var organs = Server.System<OrbitraOrganSystem>();
            var trauma = Server.System<OrbitraTraumaSystem>();
            var chemistry = Server.System<SharedSolutionContainerSystem>();
            foreach (var state in new[] { SurgeryOrganCondition.Healthy, SurgeryOrganCondition.Damaged, SurgeryOrganCondition.Nonfunctional })
            {
                var uid = SSpawnAtPosition("MobHuman", map.GridCoords);
                var stomach = organs.Installed(uid, SurgeryOrgan.Stomach)!.Value;
                trauma.SetOrganCondition(uid, organs.Installed(uid, SurgeryOrgan.Kidneys)!.Value, state);
                trauma.SetOrganCondition(uid, stomach, state);
                trauma.SetOrganCondition(uid, organs.Installed(uid, SurgeryOrgan.Liver)!.Value, SurgeryOrganCondition.Nonfunctional);

                Assert.That(chemistry.TryGetSolution(stomach, StomachSystem.DefaultSolutionName, out var meal, out var contents), Is.True);
                Assert.That(chemistry.TryAddReagent(meal!.Value, "Water", 20), Is.True);
                meals.Add(contents!);
                Assert.That(chemistry.TryGetSolution(uid, BloodstreamComponent.DefaultMetabolitesSolutionName, out var residue, out contents), Is.True);
                Assert.That(chemistry.TryAddReagent(residue!.Value, "TableSalt", 20), Is.True);
                Assert.That(chemistry.TryAddReagent(residue.Value, "Ethanol", 5), Is.True);
                residuals.Add(contents!);
            }
        });

        await Pair.RunTicksSync((int) Math.Ceiling(6.2 * SGameTiming.TickRate));
        await Server.WaitAssertion(() =>
        {
            Assert.That(residuals.Select(s => s.GetTotalPrototypeQuantity("Ethanol").Float()), Is.All.EqualTo(5f));
            CheckClearance(residuals, "TableSalt");
            CheckClearance(meals, "Water");
        });

        static void CheckClearance(List<Solution> samples, string reagent)
        {
            var processed = samples.Select(s => 20f - s.GetTotalPrototypeQuantity(reagent).Float()).ToArray();
            Assert.That(processed[1], Is.Positive);
            Assert.That(processed[0], Is.EqualTo(2f * processed[1]).Within(0.01f));
            Assert.That(processed[2], Is.Zero);
        }
    }

    [Test]
    public async Task EyeRepairAndTransplantPreserveOtherVisionLoss()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var patient = SSpawnAtPosition("MobHuman", map.GridCoords);
            var organs = Server.System<OrbitraOrganSystem>();
            var trauma = Server.System<OrbitraTraumaSystem>();
            var blindness = Server.System<BlindableSystem>();
            var eyes = organs.Installed(patient, SurgeryOrgan.Eyes)!.Value;
            var blindable = SComp<BlindableComponent>(patient);
            blindness.AdjustEyeDamage(patient, 1);
            trauma.SetOrganCondition(patient, eyes, SurgeryOrganCondition.Damaged);
            Assert.That(SComp<BlurryVisionComponent>(patient).Magnitude, Is.GreaterThan(1f));
            Assert.That(blindable.IsBlind, Is.False);
            trauma.SetOrganCondition(patient, eyes, SurgeryOrganCondition.Healthy);
            Assert.That(SComp<BlurryVisionComponent>(patient).Magnitude, Is.EqualTo(1f));
            trauma.SetOrganCondition(patient, eyes, SurgeryOrganCondition.Nonfunctional);
            Assert.That(blindable.IsBlind, Is.True);
            var containers = Server.System<SharedContainerSystem>();
            var body = SComp<BodyComponent>(patient).Organs!;
            Assert.That(containers.Remove(eyes, body), Is.True);
            Assert.That(blindable.IsBlind, Is.True);
            var donor = SSpawnAtPosition("OrganHumanEyes", map.GridCoords);
            blindness.AdjustEyeDamage(patient, blindable.MaxDamage);
            Assert.That(containers.Insert(donor, body), Is.True);
            Assert.That(blindable.IsBlind, Is.True);
            blindness.AdjustEyeDamage(patient, -blindable.MaxDamage);
            Assert.That(blindable.IsBlind, Is.False);
        });
    }

    [Test]
    public async Task RejuvenationRestoresInstalledOrgansAndPreservesBlindfold()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var patient = SSpawnAtPosition("MobHuman", map.GridCoords);
            var organs = Server.System<OrbitraOrganSystem>();
            var trauma = Server.System<OrbitraTraumaSystem>();
            var inventory = Server.System<InventorySystem>();
            var blindfold = SSpawnAtPosition("ClothingEyesBlindfold", map.GridCoords);
            Assert.That(inventory.TryEquip(patient, blindfold, "eyes", force: true), Is.True);
            foreach (var kind in OrbitraOrganSystem.Kinds)
                trauma.SetOrganCondition(patient, organs.Installed(patient, kind)!.Value, SurgeryOrganCondition.Nonfunctional);
            var stomach = organs.Installed(patient, SurgeryOrgan.Stomach)!.Value;
            Assert.That(Server.System<SharedContainerSystem>().Remove(stomach, SComp<BodyComponent>(patient).Organs!), Is.True);

            Server.System<RejuvenateSystem>().PerformRejuvenate(patient);

            foreach (var kind in OrbitraOrganSystem.Kinds)
                Assert.That(organs.Condition(patient, kind), Is.EqualTo(kind == SurgeryOrgan.Stomach
                    ? SurgeryOrganCondition.Missing : SurgeryOrganCondition.Healthy));
            Assert.That(SComp<OrbitraOrganComponent>(stomach).Condition, Is.EqualTo(SurgeryOrganCondition.Nonfunctional));
            Assert.That(organs.HearingRangeMultiplier(patient), Is.EqualTo(1f));
            Assert.That(SComp<BlindableComponent>(patient).IsBlind, Is.True);
            Assert.That(inventory.TryUnequip(patient, "eyes", force: true), Is.True);
            Assert.That(SComp<BlindableComponent>(patient).IsBlind, Is.False);
        });
    }

    [Test]
    public async Task EarDamageChangesOnlyLocalSpeechReception()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var patient = SSpawnAtPosition("MobHuman", map.GridCoords);
            var ears = Server.System<OrbitraOrganSystem>().Installed(patient, SurgeryOrgan.Ears)!.Value;
            var trauma = Server.System<OrbitraTraumaSystem>();
            var chat = Server.System<ChatSystem>();
            ChatSystem.SpeechReception Reception(ChatChannel channel, float range) =>
                chat.GetSpeechReception(channel, EntityUid.Invalid, patient, new ChatSystem.ICChatRecipientData(range, false));

            Assert.That(Reception(ChatChannel.Local, 6f), Is.EqualTo(ChatSystem.SpeechReception.Clear));
            trauma.SetOrganCondition(patient, ears, SurgeryOrganCondition.Damaged);
            Assert.That(Reception(ChatChannel.Local, 4f), Is.EqualTo(ChatSystem.SpeechReception.Clear));
            Assert.That(Reception(ChatChannel.Local, 6f), Is.EqualTo(ChatSystem.SpeechReception.OutOfRange));
            Assert.That(Reception(ChatChannel.Whisper, 1f), Is.EqualTo(ChatSystem.SpeechReception.Clear));
            Assert.That(Reception(ChatChannel.Whisper, 2f), Is.EqualTo(ChatSystem.SpeechReception.Muffled));
            Assert.That(Reception(ChatChannel.Whisper, 3f), Is.EqualTo(ChatSystem.SpeechReception.OutOfRange));
            trauma.SetOrganCondition(patient, ears, SurgeryOrganCondition.Nonfunctional);
            Assert.That(Reception(ChatChannel.Whisper, 0f), Is.EqualTo(ChatSystem.SpeechReception.Unintelligible));
            Assert.That(Server.System<SharedContainerSystem>().Remove(ears, SComp<BodyComponent>(patient).Organs!), Is.True);
            Assert.That(Reception(ChatChannel.Local, 0f), Is.EqualTo(ChatSystem.SpeechReception.Unintelligible));
            foreach (var channel in new[] { ChatChannel.Radio, ChatChannel.OOC, ChatChannel.LOOC, ChatChannel.Emotes })
                Assert.That(Reception(channel, 5f), Is.EqualTo(ChatSystem.SpeechReception.Clear));
        });
    }

    [Test]
    public async Task ChatDeliversOnlyTheSpeechAListenerCanUnderstand()
    {
        var map = await Pair.CreateTestMap();
        EntityUid? previous = null;
        var session = ServerSession!;
        try
        {
            await Server.WaitAssertion(() =>
            {
                previous = session.AttachedEntity;
                var listener = SSpawnAtPosition("MobHuman", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(session, listener);
                var headset = SSpawnAtPosition("ClothingHeadsetGrey", map.GridCoords);
                Assert.That(Server.System<InventorySystem>().TryEquip(listener, headset, "ears", force: true), Is.True);
            });
            await Pair.RunTicksSync(5);
            var historyStart = 0;
            await Client.WaitPost(() => historyStart = Client.Resolve<IUserInterfaceManager>().GetUIController<ChatUIController>().History.Count);
            var unintelligible = string.Empty;
            await Server.WaitAssertion(() =>
            {
                unintelligible = Server.Resolve<ILocalizationManager>().GetString("orbitra-surgery-organ-unintelligible");
                var listener = session.AttachedEntity!.Value;
                var speaker = SSpawnAtPosition("MobHuman", map.GridCoords);
                var far = SSpawnAtPosition("MobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(6, 0)));
                var ears = Server.System<OrbitraOrganSystem>().Installed(listener, SurgeryOrgan.Ears)!.Value;
                var trauma = Server.System<OrbitraTraumaSystem>();
                var chat = Server.System<ChatSystem>();
                void Say(EntityUid source, string message, InGameICChatType type) =>
                    chat.TrySendInGameICMessage(source, message, type, false, checkRadioPrefix: false);
                trauma.SetOrganCondition(listener, ears, SurgeryOrganCondition.Damaged);
                Say(speaker, "Near organ test", InGameICChatType.Speak);
                Say(far, "Distant organ test", InGameICChatType.Speak);
                trauma.SetOrganCondition(listener, ears, SurgeryOrganCondition.Nonfunctional);
                Say(speaker, "Hidden spoken words", InGameICChatType.Speak);
                Say(speaker, "Hidden whispered words", InGameICChatType.Whisper);
                Say(listener, "Own organ test", InGameICChatType.Speak);
                SEntMan.EnsureComponent<TelecomExemptComponent>(speaker);
                Server.System<RadioSystem>().SendRadioMessage(speaker, "Radio organ test", "Common", speaker);
            });
            await Pair.RunTicksSync(10);
            await Client.WaitAssertion(() =>
            {
                var history = Client.Resolve<IUserInterfaceManager>().GetUIController<ChatUIController>().History;
                var received = history.Skip(historyStart).Select(e => e.Msg).ToArray();
                bool HasMessage(ChatChannel channel, string words) =>
                    received.Any(m => m.Channel == channel && m.Message.Contains(words, StringComparison.OrdinalIgnoreCase));
                Assert.That(HasMessage(ChatChannel.Local, "Near organ test"), Is.True);
                Assert.That(HasMessage(ChatChannel.Local, "Distant organ test"), Is.False);
                Assert.That(HasMessage(ChatChannel.Local, "Own organ test"), Is.True);
                Assert.That(HasMessage(ChatChannel.Radio, "Radio organ test"), Is.True);
                foreach (var channel in new[] { ChatChannel.Local, ChatChannel.Whisper })
                    Assert.That(received.Any(m => m.Channel == channel && m.Message == unintelligible), Is.True);
                Assert.That(received.Any(m => m.Message.Contains("Hidden", StringComparison.OrdinalIgnoreCase) ||
                    m.WrappedMessage.Contains("Hidden", StringComparison.OrdinalIgnoreCase)), Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, previous));
        }
    }

    [Test]
    public async Task MedicalTechFabPrintsEveryDonorOrganAfterResearch()
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
            Assert.That(technology.RecipeUnlocks.Count, Is.EqualTo(OrbitraOrganSystem.Kinds.Length));
            for (var i = 0; i < 3; i++)
                Assert.That(materials.TryInsertMaterialEntity(user, SSpawnAtPosition("MaterialBiomass", map.GridCoords), fab), Is.True);
            Assert.That(materials.GetMaterialAmount(fab, "Biomass"), Is.EqualTo(300));
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
            CompleteProcedure(table, user, patient, SurgeryOrgan.Heart, SurgeryProcedure.OpenAccess);
            CompleteProcedure(table, user, patient, SurgeryOrgan.Eyes, SurgeryProcedure.OpenAccess);
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
        var zone = OrbitraOrganSystem.ZoneFor(kind);
        var steps = OrbitraSurgerySteps.Get(SProtoMan, procedure, zone);
        for (var i = 0; i < steps.Length; i++)
        {
            var item = steps[i] == OrbitraSurgerySteps.DonorOrgan ? donor!.Value :
                SSpawnAtPosition(steps[i], SComp<TransformComponent>(doctor).Coordinates);
            Hold(doctor, item);
            Assert.That(Server.System<OrbitraSurgerySystem>().TryStartStep(table, doctor, patient,
                zone, procedure, i, true, OrbitraOrganSystem.IsProcedure(procedure) ? kind : null), Is.True, $"{procedure}/{kind}/{i}");
        }
    }
}
