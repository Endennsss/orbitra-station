using System;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Buckle;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Orbitra.Surgery;

[TestFixture, NonParallelizable]
public sealed partial class OrbitraSurgeryDevTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false, Dirty = true, Map = "Dev" };

    private EntityUid StationTable()
    {
        var tables = SEntMan.AllEntityQueryEnumerator<OrbitraSurgeryTableComponent>();
        while (tables.MoveNext(out var table, out var spec))
        {
            if (!spec.FieldTable)
                return table;
        }
        throw new AssertionException("На Деве нет стационарного операционного стола.");
    }

    [Test]
    public async Task DevTableTreatsInjuriesAndReturnsTheSameWorkingHand()
    {
        var hands = Server.System<SharedHandsSystem>();
        var serverUi = Server.System<SharedUserInterfaceSystem>();
        EntityUid surgeon = default, table = default, patient = default, limb = default;
        NetEntity netTable = default, netPatient = default;
        var key = OrbitraSurgeryUiKey.Key;
        await Pair.RunTicksSync(20);
        await Server.WaitPost(() =>
        {
            var station = Server.System<ServerStationSystem>().GetStations().Single();
            Server.System<ServerGameTicker>().MakeJoinGame(ServerSession!, station, "MedicalDoctor", silent: true);
            surgeon = ServerSession!.AttachedEntity!.Value;
            table = StationTable();
            var coords = SEntMan.GetComponent<TransformComponent>(table).Coordinates;
            Server.System<SharedTransformSystem>().SetCoordinates(surgeon, coords.Offset(new Vector2(0, -0.65f)));
            patient = SEntMan.SpawnAttachedTo("MobHuman", coords);
            var trauma = SEntMan.EnsureComponent<OrbitraTraumaComponent>(patient);
            trauma.Injuries[SurgeryZone.LeftLeg] = SurgeryInjury.Fracture;
            trauma.Injuries[SurgeryZone.Torso] = SurgeryInjury.Shrapnel | SurgeryInjury.InternalBleeding;
            SEntMan.Dirty(patient, trauma);
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(patient, surgeon, table), Is.True);
            netTable = SEntMan.GetNetEntity(table);
            netPatient = SEntMan.GetNetEntity(patient);
        });
        await Pair.RunTicksSync(12);
        await Server.WaitPost(() => Assert.That(serverUi.TryOpenUi(table, key, surgeon), Is.True));
        await Pair.RunTicksSync(10);
        await Operate(SurgeryZone.LeftLeg, SurgeryProcedure.Fracture, SurgeryInjury.Fracture);
        await Operate(SurgeryZone.Torso, SurgeryProcedure.InternalBleeding, SurgeryInjury.InternalBleeding);
        await Operate(SurgeryZone.Torso, SurgeryProcedure.Shrapnel, SurgeryInjury.Shrapnel);
        await Server.WaitAssertion(() =>
        {
            var fragments = 0;
            var entities = SEntMan.AllEntityQueryEnumerator<MetaDataComponent>();
            while (entities.MoveNext(out _, out var metadata))
            {
                if (metadata.EntityPrototype?.ID == "OrbitraExtractedFragment")
                    fragments++;
            }
            Assert.That(fragments, Is.EqualTo(1));
        });
        await Operate(SurgeryZone.LeftArm, SurgeryProcedure.Amputate, SurgeryInjury.None);
        await Server.WaitAssertion(() =>
        {
            Assert.That(hands.TryGetHand(patient, "left", out _), Is.False);
            var limbs = SEntMan.AllEntityQueryEnumerator<OrbitraDetachedLimbComponent>();
            while (limbs.MoveNext(out var uid, out var part))
            {
                if (part.OriginalOwner == patient && part.Zone == SurgeryZone.LeftArm)
                    limb = uid;
            }
            Assert.That(limb.IsValid(), Is.True);
        });
        await Operate(SurgeryZone.LeftArm, SurgeryProcedure.Reattach, SurgeryInjury.Missing);
        await Server.WaitAssertion(() =>
        {
            Assert.That(hands.TryGetHand(patient, "left", out _), Is.True);
            Assert.That(SEntMan.Deleted(limb), Is.True);
        });

        async Task Operate(SurgeryZone zone, SurgeryProcedure procedure, SurgeryInjury indication)
        {
            var tools = OrbitraSurgerySteps.Get(procedure);
            for (var step = 0; step < tools.Length; step++)
            {
                await Server.WaitPost(() =>
                {
                    if (hands.GetActiveItem(surgeon) is { } previous)
                    {
                        // Смена инструмента клавишей Q, пока курсор над окном стола.
                        Assert.That(hands.TryDrop(surgeon, previous, EntityCoordinates.Invalid), Is.True);
                        var transform = Server.System<SharedTransformSystem>();
                        Assert.That(transform.GetMapCoordinates(previous).InRange(transform.GetMapCoordinates(surgeon), 0.05f),
                            Is.True, "Инструмент должен остаться у ног хирурга.");
                    }
                    var item = tools[step] == "DetachedBody" ? limb : SEntMan.SpawnAttachedTo(tools[step],
                        SEntMan.GetComponent<TransformComponent>(surgeon).Coordinates);
                    Assert.That(hands.TryPickup(surgeon, item), Is.True);
                });
                await Pair.RunTicksSync(6);
                await Client.WaitPost(() => Client.System<SharedUserInterfaceSystem>().ClientSendUiMessage(
                    CEntMan.GetEntity(netTable), key, new OrbitraSurgeryStepMessage(netPatient, zone, procedure, step, true)));
                await Pair.RunTicksSync(6);
                var ticks = 0;
                await Server.WaitAssertion(() =>
                {
                    Assert.That(serverUi.TryGetUiState<OrbitraSurgeryState>(table, key, out var state), Is.True);
                    Assert.That(state!.Busy, Is.True, $"Шаг {procedure}/{step}");
                    var period = Server.ResolveDependency<IGameTiming>().TickPeriod;
                    ticks = (int) Math.Ceiling(state.Duration / period) + 4;
                });
                await Pair.RunTicksSync(ticks);
            }
            await Client.WaitAssertion(() =>
            {
                var ui = Client.System<SharedUserInterfaceSystem>();
                Assert.That(ui.TryGetUiState<OrbitraSurgeryState>(CEntMan.GetEntity(netTable), key, out var state), Is.True);
                Assert.That(state!.Busy, Is.False);
                Assert.That(state.ActiveProcedure, Is.Null);
                if (procedure == SurgeryProcedure.Amputate)
                    Assert.That(state.Injuries[zone].HasFlag(SurgeryInjury.Missing), Is.True);
                else
                    Assert.That(state.Injuries[zone] & indication, Is.EqualTo(SurgeryInjury.None));
            });
        }
    }
}
