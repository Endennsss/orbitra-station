using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Lavaland;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.NodeGroups;
using Content.Shared.Light.Components;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Wall;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra.Lavaland;

public sealed class OrbitraLavalandOutpostPowerTest : GameTest
{
    private EntityUid _surface;
    private EntityUid _outpost;

    [TearDown]
    public async Task RemoveSurface()
    {
        if (_surface.Valid)
            await Server.WaitPost(() => SEntMan.DeleteEntity(_surface));
    }

    [Test]
    public async Task GeneratorsRestoreEveryLightingCircuitAfterBatteriesRunDry()
    {
        var batteries = SEntMan.System<BatterySystem>();
        var devices = new List<EntityUid>();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<OrbitraLavalandSurfaceSystem>().TryCreate(23, out _surface, out _outpost));
            var children = SEntMan.GetComponent<TransformComponent>(_outpost).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                devices.Add(child);
                if (SEntMan.HasComponent<PowerNetworkBatteryComponent>(child))
                    batteries.SetCharge(child, 0);
            }
        });

        await Pair.RunSeconds(15);
        await Server.WaitAssertion(() =>
        {
            var substation = devices.Single(uid => SEntMan.HasComponent<BatteryDischargerComponent>(uid));
            var highVoltage = SEntMan.GetComponent<BatteryChargerComponent>(substation).Net;
            var mediumVoltage = SEntMan.GetComponent<BatteryDischargerComponent>(substation).Net;
            Assert.That(highVoltage, Is.Not.Null);
            Assert.That(mediumVoltage, Is.Not.Null);
            var generators = devices.Where(SEntMan.HasComponent<PowerSupplierComponent>).ToArray();
            Assert.That(generators, Is.Not.Empty);
            foreach (var generator in generators)
                Assert.That(SEntMan.GetComponent<PowerSupplierComponent>(generator).Net, Is.SameAs(highVoltage));

            var apcs = devices.Where(SEntMan.HasComponent<ApcComponent>).ToArray();
            Assert.That(apcs, Has.Length.EqualTo(4));
            foreach (var apc in apcs)
            {
                Assert.That(SEntMan.GetComponent<BatteryChargerComponent>(apc).Net, Is.SameAs(mediumVoltage), Position(apc));
                Assert.That(batteries.GetCharge(apc).Charge, Is.Positive, Position(apc));
            }

            var circuits = apcs.Select(apc => SEntMan.GetComponent<ApcComponent>(apc).Net).ToHashSet();
            Assert.That(circuits, Has.Count.EqualTo(apcs.Length));
            var cableCircuits = devices
                .Where(uid => SEntMan.TryGetComponent<CableComponent>(uid, out var cable) && cable.CableType == CableType.Apc)
                .Select(uid => SEntMan.GetComponent<ApcPowerProviderComponent>(uid).Net)
                .ToHashSet();
            Assert.That(cableCircuits, Is.EquivalentTo(circuits));
            var grid = SEntMan.GetComponent<MapGridComponent>(_outpost);
            var lights = devices.Where(SEntMan.HasComponent<PoweredLightComponent>).ToArray();
            Assert.That(lights.Any(uid => PositionOnGrid(uid) == new Vector2i(-5, 0)), Is.True,
                "В переходе между корпусами нужен рабочий свет.");
            var litCircuits = new HashSet<IApcNet>();
            foreach (var lamp in lights)
            {
                var receiver = SEntMan.GetComponent<ApcPowerReceiverComponent>(lamp);
                Assert.That(receiver.Provider?.Net, Is.Not.Null, Position(lamp));
                litCircuits.Add(receiver.Provider!.Net!);
                Assert.That(receiver.PowerReceived, Is.Positive, Position(lamp));
                Assert.That(SEntMan.GetComponent<PoweredLightComponent>(lamp).CurrentLit, Is.True, Position(lamp));
                Assert.That(SEntMan.GetComponent<PointLightComponent>(lamp).Enabled, Is.True, Position(lamp));

                var tile = PositionOnGrid(lamp);
                var mount = tile + SEntMan.GetComponent<TransformComponent>(lamp).LocalRotation
                    .RotateDir(Direction.North).ToIntVec();
                Assert.That(HasWall(grid, tile), Is.False, Position(lamp));
                Assert.That(HasWall(grid, mount), Is.True, Position(lamp));
            }
            Assert.That(litCircuits, Is.EquivalentTo(circuits));
        });
    }

    private bool HasWall(MapGridComponent grid, Vector2i tile)
    {
        foreach (var uid in SEntMan.System<SharedMapSystem>().GetAnchoredEntities(_outpost, grid, tile))
        {
            if (SEntMan.HasComponent<WallComponent>(uid))
                return true;
        }
        return false;
    }

    private Vector2i PositionOnGrid(EntityUid uid) => SEntMan.GetComponent<TransformComponent>(uid).LocalPosition.Floored();
    private string Position(EntityUid uid) => $"{SEntMan.GetComponent<MetaDataComponent>(uid).EntityName} {PositionOnGrid(uid)}";
}
