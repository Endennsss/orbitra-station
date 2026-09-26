using System.Numerics;
using System.Linq;
using Content.Server._Orbitra.Ratvar;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Construction.Components;
using Content.Shared.Access.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed partial class OrbitraRatvarFloorTest
{
    [TestCase("SilverDoor", "success")]
    [TestCase("GoldDoor", "success")]
    [TestCase("PlasmaDoor", "success")]
    [TestCase("SilverDoor", "open")]
    [TestCase("SilverDoor", "access")]
    [TestCase("SilverDoor", "contents")]
    [TestCase("SilverDoor", "delete")]
    [TestCase("SilverDoor", "parallel")]
    [TestCase("Airlock", "unsupported")]
    [TestCase("WoodDoor", "unsupported")]
    public async Task DoorConversionPreservesInfrastructureAndRestrictions(string prototype, string scenario)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, tool = default, door = default, cable = default, pipe = default, item = default;
        OrbitraRatvarRuleComponent cult = null!;
        var started = false;
        var secondStarted = false;
        await Server.WaitPost(() =>
        {
            PrepareFloor(map.Grid, map.Tile.Tile);
            var center = map.GridCoords.Offset(new Vector2(0.5f));
            user = CreateCultist(center.Offset(-Vector2.UnitX), out cult);
            cult.Energy = 400;
            tool = EquipTool(user, center);
            door = SEntMan.SpawnEntity(prototype, center);
            cable = SEntMan.SpawnEntity("CableHV", center);
            pipe = SEntMan.SpawnEntity("GasPipeStraight", center);
            item = SEntMan.SpawnEntity("Crowbar", center);
            var fabricator = Server.System<OrbitraRatvarFabricatorSystem>();
            if (scenario == "unsupported")
                started = fabricator.TryStartDoor((tool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool)), user, door);
            else
            {
                var interaction = new AfterInteractEvent(user, tool, door, center, true);
                SEntMan.EventBus.RaiseLocalEvent(tool, interaction);
                started = SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool).Pending != null;
            }
            if (scenario == "parallel")
            {
                var other = CreateCultist(center.Offset(Vector2.UnitY), out _, cult.Members.First());
                cult.Energy = 400;
                var otherTool = EquipTool(other, center);
                secondStarted = fabricator.TryStartDoor(
                    (otherTool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(otherTool)), other, door);
            }
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.EqualTo(scenario != "unsupported"));
            if (scenario == "parallel")
                Assert.That(secondStarted, Is.True);
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitPost(() =>
        {
            switch (scenario)
            {
                case "open": Server.System<SharedDoorSystem>().TryOpen(door); break;
                case "access": SEntMan.AddComponent<AccessReaderComponent>(door); break;
                case "contents":
                    var containers = Server.System<SharedContainerSystem>();
                    containers.Insert(item, containers.EnsureContainer<ContainerSlot>(door, "door-test"));
                    break;
                case "delete": SEntMan.DeleteEntity(door); break;
            }
        });
        await Pair.RunSeconds(7);
        EntityUid converted = default;
        var succeeds = scenario is "success" or "parallel";
        await Server.WaitAssertion(() =>
        {
            Assert.That(cult.Energy, Is.EqualTo(succeeds ? 200 : 400));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool).Pending, Is.Null);
            Assert.That(SEntMan.EntityExists(door), Is.EqualTo(!succeeds && scenario != "delete"));
            foreach (var entity in new[] { cable, pipe, item })
                Assert.That(SEntMan.EntityExists(entity), Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(cable).Anchored, Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(pipe).Anchored, Is.True);
            var count = 0;
            foreach (var entity in Server.System<SharedMapSystem>().GetAnchoredEntities(map.Grid, map.Grid.Comp, map.Tile.GridIndices))
            {
                if (SEntMan.GetComponent<MetaDataComponent>(entity).EntityPrototype?.ID != "OrbitraRatvarDoor")
                    continue;
                converted = entity;
                count++;
                var construction = SEntMan.GetComponent<ConstructionComponent>(entity);
                Assert.That(construction.Graph.Id, Is.EqualTo("OrbitraRatvarDoor"));
                Assert.That(construction.Node, Is.EqualTo("door"));
                Assert.That(SEntMan.GetComponent<DoorComponent>(entity).State, Is.EqualTo(DoorState.Closed));
            }
            Assert.That(count, Is.EqualTo(succeeds ? 1 : 0));
            if (succeeds)
                Assert.That(Server.System<AtmosphereSystem>().IsTileAirBlocked(map.Grid, map.Tile.GridIndices), Is.True);
        });
        if (!succeeds)
            return;

        // Проверяем работоспособность результата, а не только его прототип.
        var opened = false;
        await Server.WaitPost(() => opened = Server.System<SharedDoorSystem>().TryOpen(converted, user: user));
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(opened, Is.True);
            Assert.That(SEntMan.GetComponent<DoorComponent>(converted).State, Is.EqualTo(DoorState.Open));
            Assert.That(Server.System<AtmosphereSystem>().IsTileAirBlocked(map.Grid, map.Tile.GridIndices), Is.False);
        });
        var closed = false;
        await Server.WaitPost(() => closed = Server.System<SharedDoorSystem>().TryClose(converted, user: user));
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(closed, Is.True);
            Assert.That(SEntMan.GetComponent<DoorComponent>(converted).State, Is.EqualTo(DoorState.Closed));
            Assert.That(Server.System<AtmosphereSystem>().IsTileAirBlocked(map.Grid, map.Tile.GridIndices), Is.True);
        });
    }
}
