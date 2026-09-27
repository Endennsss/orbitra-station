using System.Numerics;
using System.Linq;
using Content.Server._Orbitra.Ratvar;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Construction.Components;
using Content.Shared.Access.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
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
    [TestCase("Airlock", "success", Category = "OrbitraRatvarExpansion")]
    [TestCase("AirlockMedicalLocked", "success", Category = "OrbitraRatvarExpansion")]
    [TestCase("AirlockCommandLocked", "success", Category = "OrbitraRatvarExpansion")]
    [TestCase("AirlockGlass", "success", Category = "OrbitraRatvarExpansion")]
    [TestCase("Airlock", "contents", Category = "OrbitraRatvarExpansion")]
    [TestCase("Airlock", "parallel", Category = "OrbitraRatvarExpansion")]
    [TestCase("WoodDoor", "unsupported")]
    public async Task DoorConversionPreservesInfrastructureAndRestrictions(string prototype, string scenario)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, tool = default, door = default, cable = default, pipe = default, item = default;
        EntityUid? board = null;
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
            if (Server.System<SharedContainerSystem>().TryGetContainer(door, "board", out var electronics))
                board = electronics.ContainedEntities.Single();
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
            if (board is { } preservedBoard)
            {
                Assert.That(SEntMan.EntityExists(preservedBoard), Is.True, "Conversion must not delete the original electronics.");
                if (succeeds)
                {
                    Assert.That(Server.System<SharedContainerSystem>().TryGetContainer(converted, "board", out var electronics), Is.True);
                    Assert.That(electronics!.ContainedEntities, Does.Contain(preservedBoard));
                }
            }
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
        if (board is not { } originalBoard)
            return;
        await Server.WaitPost(() => Server.System<DamageableSystem>().TryChangeDamage(converted,
            new DamageSpecifier(SProtoMan.Index(WallTestBlunt), 1000), ignoreResistances: true));
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(originalBoard), Is.True);
            Assert.That(Server.System<SharedContainerSystem>().IsEntityInContainer(originalBoard), Is.False,
                "Destroying the converted door must release the preserved electronics.");
        });
    }
}
