#nullable enable
using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.Surgery;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Surgery;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle;
using Content.Shared.CartridgeLoader;
using Content.Shared.Chemistry.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Medical.HealthAnalyzer;
using Content.Shared.Mobs;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Temperature.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Orbitra.Surgery;

[TestFixture, NonParallelizable]
public sealed class OrbitraSurgeryMonitorTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false, Dirty = true, Map = "Dev" };

    [Test]
    public async Task OnlyTheCarrierReceivesReadingsFromTheDevTable()
    {
        var ui = Server.System<SharedUserInterfaceSystem>();
        var hands = Server.System<SharedHandsSystem>();
        var inventory = Server.System<InventorySystem>();
        var blood = Server.System<BloodstreamSystem>();
        var slots = Server.System<ItemSlotsSystem>();
        var key = OrbitraSurgeryUiKey.Key;
        EntityUid doctor = default, colleague = default, patient = default, table = default;
        EntityUid bag = default, pda = default, cartridge = default;
        NetEntity netTable = default, netDoctor = default, netColleague = default;

        await Pair.RunTicksSync(20);
        await Server.WaitAssertion(() =>
        {
            var tables = SEntMan.AllEntityQueryEnumerator<OrbitraSurgeryTableComponent>();
            while (tables.MoveNext(out table, out var station))
            {
                if (!station.FieldTable)
                    break;
            }
            Assert.That(table.IsValid(), Is.True);
            var coords = SComp<TransformComponent>(table).Coordinates;
            var dev = Server.System<ServerStationSystem>().GetStations().Single();
            Server.System<ServerGameTicker>().MakeJoinGame(ServerSession!, dev, "Passenger", silent: true);
            doctor = ServerSession!.AttachedEntity!.Value;
            Server.System<SharedTransformSystem>().SetCoordinates(doctor, coords.Offset(new Vector2(0, -0.65f)));
            if (inventory.TryGetSlotEntity(doctor, "back", out _))
                Assert.That(inventory.TryUnequip(doctor, "back", force: true), Is.True);
            if (hands.GetActiveItem(doctor) is { } startingItem)
                Assert.That(hands.TryDrop(doctor, startingItem), Is.True);
            colleague = SSpawnAtPosition("MobHuman", coords.Offset(new Vector2(0.5f, -0.65f)));
            patient = SSpawnAtPosition("MobHuman", coords);
            netTable = SEntMan.GetNetEntity(table);
            netDoctor = SEntMan.GetNetEntity(doctor);
            netColleague = SEntMan.GetNetEntity(colleague);
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(patient, doctor, table), Is.True);
            blood.ChangeBloodRefreshAmount(patient, 0);

            var storage = Server.System<SharedStorageSystem>();
            var kit = SSpawnAtPosition("Medkit", coords);
            var analyzer = SSpawnAtPosition("HandheldHealthAnalyzer", coords);
            bag = SSpawnAtPosition("ClothingBackpack", coords);
            Assert.That(storage.Insert(kit, analyzer, out _), Is.True);
            Assert.That(storage.Insert(bag, kit, out _), Is.True);
            Assert.That(inventory.TryEquip(colleague, bag, "back"), Is.True);

            pda = SSpawnAtPosition("PassengerPDA", coords);
            cartridge = SSpawnAtPosition("MedTekCartridge", coords);
            Assert.That(hands.TryPickup(doctor, pda), Is.True);
            Assert.That(SEntMan.HasComponent<HealthAnalyzerComponent>(pda), Is.False);
        });
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(ui.TryOpenUi(table, key, doctor), Is.True);
            Assert.That(ui.TryOpenUi(table, key, colleague), Is.True);
        });
        await Pair.RunTicksSync(8);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reading(colleague).Patient, Is.EqualTo(SEntMan.GetNetEntity(patient)));
            Assert.That(Reading(colleague).Trend, Is.EqualTo(OrbitraBloodTrend.Unknown));
            Assert.That(SEntMan.HasComponent<OrbitraSurgeryMonitorComponent>(doctor), Is.False);
        });
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.HasComponent<OrbitraSurgeryMonitorComponent>(CEntMan.GetEntity(netColleague)), Is.False);
            Assert.That(CEntMan.HasComponent<OrbitraSurgeryMonitorComponent>(CEntMan.GetEntity(netDoctor)), Is.False);
        });
        await AssertPanel(false);

        await Server.WaitAssertion(() =>
        {
            Assert.That(inventory.TryUnequip(colleague, "back"), Is.True);
            Assert.That(SEntMan.HasComponent<OrbitraSurgeryMonitorComponent>(colleague), Is.False);
            Assert.That(inventory.TryEquip(doctor, bag, "back"), Is.True);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            var reading = Reading(doctor);
            Assert.That(reading.State, Is.EqualTo(MobState.Alive));
            Assert.That(reading.Temperature, Is.EqualTo(SComp<TemperatureComponent>(patient).Temperature).Within(1f));
            Assert.That(reading.BloodLevel, Is.EqualTo(blood.GetBloodLevel(patient)));
            Assert.That(reading.Trend, Is.EqualTo(OrbitraBloodTrend.Unknown));
            Assert.That(blood.TryBleedOut(patient, 0.6f), Is.True);
        });
        await AssertPanel(true);
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reading(doctor).Trend, Is.EqualTo(OrbitraBloodTrend.Falling));
            float referenceVolume = (float) SComp<BloodstreamComponent>(patient).BloodReferenceSolution.Volume;
            var expected = 1f - 0.6f / referenceVolume;
            Assert.That(Reading(doctor).BloodLevel, Is.EqualTo(expected).Within(0.00001f));
            // Собственная кровь сохраняет ДНК и остаётся в кровотоке после переливания.
            var transfusion = new Solution([new("Blood", 0.6f, blood.GetEntityBloodData(patient))]);
            Assert.That(blood.TryAddToBloodstream(patient, transfusion), Is.True);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() => Assert.That(Reading(doctor).Trend, Is.EqualTo(OrbitraBloodTrend.Rising)));
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reading(doctor).Trend, Is.EqualTo(OrbitraBloodTrend.Stable));
            var injury = SEntMan.EnsureComponent<OrbitraTraumaComponent>(patient);
            injury.Injuries[SurgeryZone.LeftLeg] = SurgeryInjury.Fracture;
            SEntMan.Dirty(patient, injury);
            Assert.That(hands.TryDrop(doctor, pda), Is.True);
            var retractor = SSpawnAtPosition("Retractor", SComp<TransformComponent>(doctor).Coordinates);
            Assert.That(hands.TryPickup(doctor, retractor), Is.True);
            var surgery = Server.System<OrbitraSurgerySystem>();
            SEntMan.EnsureComponent<OrbitraSurgicalAccessComponent>(patient).Zones[SurgeryZone.LeftLeg] = SurgeryAccess.Open;
            Assert.That(surgery.TryStartStep(table, doctor, patient, SurgeryZone.LeftLeg, SurgeryProcedure.Fracture, 0, false), Is.True);
            var pending = SComp<OrbitraSurgeryOperationComponent>(patient).Pending;
            Assert.That(inventory.TryUnequip(doctor, "back"), Is.True);
            Assert.That(SEntMan.HasComponent<OrbitraSurgeryMonitorComponent>(doctor), Is.False);
            Assert.That(Server.System<SharedDoAfterSystem>().IsRunning(pending), Is.True);
        });
        await Pair.RunTicksSync(6);
        await AssertPanel(false);
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<OrbitraSurgeryOperationComponent>(patient).Step, Is.EqualTo(1));
            Assert.That(SEntMan.HasComponent<OrbitraSurgeryMonitorComponent>(doctor), Is.False);
            Assert.That(hands.TryDrop(doctor, hands.GetActiveItem(doctor)!.Value), Is.True);
            Assert.That(hands.TryPickup(doctor, pda), Is.True);
            Assert.That(slots.TryInsert(pda, CartridgeLoaderComponent.CartridgeSlotId, cartridge, null), Is.True);
            Assert.That(SEntMan.HasComponent<HealthAnalyzerComponent>(pda), Is.True);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reading(doctor).Trend, Is.EqualTo(OrbitraBloodTrend.Unknown));
            Assert.That(hands.TryDrop(doctor, pda), Is.True);
            Assert.That(SEntMan.HasComponent<OrbitraSurgeryMonitorComponent>(doctor), Is.False);
            Assert.That(inventory.TryEquip(colleague, pda, "pocket1", force: true), Is.True);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reading(colleague).Trend, Is.EqualTo(OrbitraBloodTrend.Unknown));
            Assert.That(slots.TryEject(pda, CartridgeLoaderComponent.CartridgeSlotId, null, out var removed), Is.True);
            Assert.That(removed, Is.EqualTo(cartridge));
            Assert.That(SEntMan.HasComponent<HealthAnalyzerComponent>(pda), Is.False);
            Assert.That(SEntMan.HasComponent<OrbitraSurgeryMonitorComponent>(colleague), Is.False);
            Assert.That(slots.TryInsert(pda, CartridgeLoaderComponent.CartridgeSlotId, cartridge, null), Is.True);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reading(colleague).Trend, Is.EqualTo(OrbitraBloodTrend.Unknown));
            ui.CloseUi(table, key, colleague);
            Assert.That(SEntMan.HasComponent<OrbitraSurgeryMonitorComponent>(colleague), Is.False);
            Assert.That(inventory.TryEquip(doctor, bag, "back"), Is.True);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reading(doctor).Trend, Is.EqualTo(OrbitraBloodTrend.Unknown));
            Assert.That(Server.System<SharedBuckleSystem>().TryUnbuckle(patient, doctor), Is.True);
            Assert.That(SEntMan.HasComponent<OrbitraSurgeryMonitorComponent>(doctor), Is.False);
        });

        OrbitraSurgeryReading Reading(EntityUid actor) => SComp<OrbitraSurgeryMonitorComponent>(actor).Readings[netTable];

        async Task AssertPanel(bool visible)
        {
            await Client.WaitAssertion(() =>
            {
                var clientUi = Client.System<SharedUserInterfaceSystem>();
                Assert.That(clientUi.TryGetOpenUi<OrbitraSurgeryBoundUserInterface>(CEntMan.GetEntity(netTable), key, out var bui), Is.True);
                OrbitraWindowLifecycleTest.Frame(Client.ResolveDependency<IUserInterfaceManager>(), 0.025f);
                var panel = bui!.Window!.FindControl<RichTextLabel>("PatientMonitor");
                Assert.That(panel.Visible, Is.EqualTo(visible));
                if (visible)
                    Assert.That(panel.GetMessage(), Does.Contain("°C"));
                else
                    Assert.That(panel.GetMessage(), Is.Null.Or.Empty);
            });
        }
    }
}
