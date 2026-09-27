using System.Numerics;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.Atmos.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed partial class OrbitraRatvarFloorTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";
    private static readonly ProtoId<ContentTileDefinition> BrassFloor = "OrbitraRatvarFloor";

    // Изоляция от общего набора тестовых прототипов, как у ремонтного режима.
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [TestCase("Plating")]
    [TestCase("PlatingSnow")]
    [TestCase("FloorSteel", Category = "OrbitraRatvarExpansion")]
    [TestCase("FloorWhite", Category = "OrbitraRatvarExpansion")]
    [TestCase("FloorDark", Category = "OrbitraRatvarExpansion")]
    [TestCase("FloorWood", Category = "OrbitraRatvarExpansion")]
    public async Task FabricationPreservesInfrastructureAndBase(string source)
    {
        var map = await Pair.CreateTestMap();
        EntityUid tool = default, cable = default, pipe = default, item = default;
        OrbitraRatvarRuleComponent cult = null!;
        var started = false;
        var duplicate = true;
        var original = new Tile(Server.Resolve<ITileDefinitionManager>()[source].TileId);
        await Server.WaitPost(() =>
        {
            PrepareFloor(map.Grid, original);
            var user = CreateCultist(map.GridCoords.Offset(new Vector2(-0.5f, 0.5f)), out cult);
            tool = EquipTool(user, map.GridCoords);
            cable = SEntMan.SpawnEntity("CableHV", map.GridCoords);
            pipe = SEntMan.SpawnEntity("GasPipeStraight", map.GridCoords);
            item = SEntMan.SpawnEntity("Crowbar", map.GridCoords);
            var interaction = new AfterInteractEvent(user, tool, null, map.GridCoords, true);
            SEntMan.EventBus.RaiseLocalEvent(tool, interaction);
            started = SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool).Pending != null;
            duplicate = Server.System<OrbitraRatvarFabricatorSystem>().TryStartFloor(
                (tool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool)), user, map.GridCoords);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(duplicate, Is.False);
            Assert.That(cult.Energy, Is.EqualTo(40));
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedMapSystem>().GetTileRef(map.Grid, map.Grid.Comp, map.GridCoords).Tile.TypeId,
                Is.EqualTo(SProtoMan.Index(BrassFloor).TileId));
            Assert.That(cult.Energy, Is.EqualTo(20));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool).Pending, Is.Null);
            foreach (var entity in new[] { cable, pipe, item })
                Assert.That(SEntMan.EntityExists(entity), Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(cable).Anchored, Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(pipe).Anchored, Is.True);
            var atmos = Server.System<AtmosphereSystem>();
            Assert.That(atmos.IsTileSpace((map.Grid.Owner, null), map.MapUid, map.Tile.GridIndices), Is.False);
            Assert.That(atmos.IsTileAirBlocked(map.Grid, map.Tile.GridIndices), Is.False);
        });
        var removed = false;
        await Server.WaitPost(() => removed = Server.System<TileSystem>().DeconstructTile(
            Server.System<SharedMapSystem>().GetTileRef(map.Grid, map.Grid.Comp, map.GridCoords), spawnItem: false));
        await Server.WaitAssertion(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(Server.System<SharedMapSystem>().GetTileRef(map.Grid, map.Grid.Comp, map.GridCoords).Tile.TypeId,
                Is.EqualTo(original.TypeId));
        });
    }

    [TestCase("tile")]
    [TestCase("drop")]
    [TestCase("energy")]
    [TestCase("move")]
    [TestCase("mind")]
    [TestCase("container")]
    [TestCase("tool-delete")]
    [TestCase("blocked")]
    public async Task ChangedConditionsCancelWithoutPayment(string scenario)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, tool = default;
        OrbitraRatvarRuleComponent cult = null!;
        var original = new Tile(Server.Resolve<ITileDefinitionManager>()["Plating"].TileId);
        var started = false;
        await Server.WaitPost(() =>
        {
            PrepareFloor(map.Grid, original);
            user = CreateCultist(map.GridCoords.Offset(new Vector2(-0.5f, 0.5f)), out cult);
            tool = EquipTool(user, map.GridCoords);
            started = Server.System<OrbitraRatvarFabricatorSystem>().TryStartFloor(
                (tool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool)), user, map.GridCoords);
        });
        await Server.WaitAssertion(() => Assert.That(started, Is.True));
        await Pair.RunSeconds(0.5f);
        await Server.WaitPost(() =>
        {
            switch (scenario)
            {
                case "tile": Server.System<SharedMapSystem>().SetTile(map.Grid, map.Tile.GridIndices, new Tile(SProtoMan.Index(BrassFloor).TileId)); break;
                case "drop": Server.System<SharedHandsSystem>().TryDrop(user, tool); break;
                case "energy": cult.Energy = 19; break;
                case "move": Server.System<SharedTransformSystem>().SetCoordinates(user, map.GridCoords.Offset(new Vector2(-2, 1))); break;
                case "mind":
                    Server.System<MindSystem>().TryGetMind(user, out var mind, out _);
                    Server.System<MindSystem>().TransferTo(mind, null);
                    break;
                case "container":
                    var box = SEntMan.SpawnEntity("CrateGenericSteel", map.GridCoords.Offset(-Vector2.UnitX));
                    var containers = Server.System<SharedContainerSystem>();
                    containers.Insert(user, containers.EnsureContainer<ContainerSlot>(box, "orbitra-floor-test"));
                    break;
                case "tool-delete": SEntMan.DeleteEntity(tool); break;
                case "blocked": SEntMan.SpawnEntity("WallSolid", map.GridCoords); break;
            }
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(cult.Energy, Is.EqualTo(scenario == "energy" ? 19 : 40));
            if (SEntMan.EntityExists(tool))
                Assert.That(SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool).Pending, Is.Null);
            Assert.That(Server.System<SharedMapSystem>().GetTileRef(map.Grid, map.Grid.Comp, map.GridCoords).Tile.TypeId,
                Is.EqualTo(scenario == "tile" ? SProtoMan.Index(BrassFloor).TileId : original.TypeId));
        });
    }

    [Test]
    public async Task UnsupportedFloorAndConcurrentFabricationCannotChargeTwice()
    {
        var map = await Pair.CreateTestMap();
        OrbitraRatvarRuleComponent cult = null!;
        var started = false;
        var secondStarted = false;
        var unsupported = true;
        await Server.WaitPost(() =>
        {
            PrepareFloor(map.Grid, new Tile(Server.Resolve<ITileDefinitionManager>()["Plating"].TileId));
            var user = CreateCultist(map.GridCoords.Offset(new Vector2(-0.5f, 0.5f)), out cult);
            var tool = EquipTool(user, map.GridCoords);
            var system = Server.System<OrbitraRatvarFabricatorSystem>();
            var ent = (tool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool));
            var maps = Server.System<SharedMapSystem>();
            maps.SetTile(map.Grid, map.Tile.GridIndices, new Tile(SProtoMan.Index(BrassFloor).TileId));
            unsupported = system.TryStartFloor(ent, user, map.GridCoords);
            maps.SetTile(map.Grid, map.Tile.GridIndices, new Tile(Server.Resolve<ITileDefinitionManager>()["Plating"].TileId));
            started = system.TryStartFloor(ent, user, map.GridCoords);
            var other = CreateCultist(map.GridCoords.Offset(Vector2.UnitY), out _, cult.Members.First());
            var otherTool = EquipTool(other, map.GridCoords);
            secondStarted = system.TryStartFloor((otherTool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(otherTool)), other, map.GridCoords);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(unsupported, Is.False);
            Assert.That(started && secondStarted, Is.True);
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() => Assert.That(cult.Energy, Is.EqualTo(20)));
    }

    [TestCase("move-user")]
    [TestCase("move-grid")]
    [TestCase("delete-grid")]
    public async Task GridChangesCancelWithoutPayment(string change)
    {
        var source = await Pair.CreateTestMap();
        var destination = await Pair.CreateTestMap();
        var original = new Tile(Server.Resolve<ITileDefinitionManager>()["Plating"].TileId);
        EntityUid user = default, tool = default;
        OrbitraRatvarRuleComponent cult = null!;
        var started = false;
        await Server.WaitPost(() =>
        {
            PrepareFloor(source.Grid, original);
            PrepareFloor(destination.Grid, original);
            user = CreateCultist(source.GridCoords.Offset(new Vector2(-0.5f, 0.5f)), out cult);
            tool = EquipTool(user, source.GridCoords);
            started = Server.System<OrbitraRatvarFabricatorSystem>().TryStartFloor(
                (tool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool)), user, source.GridCoords);
        });
        await Server.WaitAssertion(() => Assert.That(started, Is.True));
        await Pair.RunSeconds(0.5f);
        await Server.WaitPost(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            if (change == "move-grid")
                transform.SetCoordinates(source.Grid, new EntityCoordinates(destination.MapUid, Vector2.Zero));
            else
                transform.SetCoordinates(user, destination.GridCoords.Offset(new Vector2(-0.5f, 0.5f)));

            // Сначала спасаем пользователя с инструментом, затем удаляем исходный грид.
            if (change == "delete-grid")
                SEntMan.DeleteEntity(source.Grid);
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(user), Is.True);
            Assert.That(SEntMan.EntityExists(tool), Is.True);
            Assert.That(cult.Energy, Is.EqualTo(40));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool).Pending, Is.Null);
            var maps = Server.System<SharedMapSystem>();
            Assert.That(maps.GetTileRef(destination.Grid, destination.Grid.Comp, destination.GridCoords).Tile.TypeId,
                Is.EqualTo(original.TypeId));
            if (change != "delete-grid")
                Assert.That(maps.GetTileRef(source.Grid, source.Grid.Comp, source.GridCoords).Tile.TypeId,
                    Is.EqualTo(original.TypeId));
        });
    }

    [TestCase(0)]
    [TestCase(20)]
    public async Task SeparateCultsCannotSpendEachOthersFloorEnergy(int secondEnergy)
    {
        var map = await Pair.CreateTestMap();
        var firstTile = map.GridCoords;
        var secondTile = map.GridCoords.Offset(Vector2.One);
        OrbitraRatvarRuleComponent first = null!, second = null!;
        var firstStarted = false;
        var secondStarted = false;
        var original = new Tile(Server.Resolve<ITileDefinitionManager>()["Plating"].TileId);
        await Server.WaitPost(() =>
        {
            PrepareFloor(map.Grid, original);
            var firstUser = CreateCultist(firstTile.Offset(new Vector2(-0.5f, 0.5f)), out first);
            var secondUser = CreateCultist(secondTile.Offset(new Vector2(-0.5f, 0.5f)), out second);
            second.Energy = secondEnergy;
            var firstTool = EquipTool(firstUser, firstTile);
            var secondTool = EquipTool(secondUser, secondTile);
            var fabricator = Server.System<OrbitraRatvarFabricatorSystem>();
            firstStarted = fabricator.TryStartFloor(
                (firstTool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(firstTool)), firstUser, firstTile);
            secondStarted = fabricator.TryStartFloor(
                (secondTool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(secondTool)), secondUser, secondTile);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(firstStarted, Is.True);
            Assert.That(secondStarted, Is.EqualTo(secondEnergy >= 20));
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(first.Energy, Is.EqualTo(20));
            Assert.That(second.Energy, Is.Zero);
            var maps = Server.System<SharedMapSystem>();
            Assert.That(maps.GetTileRef(map.Grid, map.Grid.Comp, firstTile).Tile.TypeId,
                Is.EqualTo(SProtoMan.Index(BrassFloor).TileId));
            Assert.That(maps.GetTileRef(map.Grid, map.Grid.Comp, secondTile).Tile.TypeId,
                Is.EqualTo(secondEnergy >= 20 ? SProtoMan.Index(BrassFloor).TileId : original.TypeId));
        });
    }

    private EntityUid CreateCultist(EntityCoordinates coordinates, out OrbitraRatvarRuleComponent cult, EntityUid? existingMind = null)
    {
        EntityUid rule;
        var roles = Server.System<RoleSystem>();
        if (existingMind is { } existing)
        {
            roles.MindHasRole<OrbitraRatvarRoleComponent>(existing, out var previousRole);
            rule = previousRole!.Value.Comp2.Rule!.Value;
        }
        else
            Server.System<GameTicker>().StartGameRule(CultRule, out rule);
        cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
        cult.Energy = 40;
        var user = SEntMan.SpawnEntity("MobHuman", coordinates);
        var minds = Server.System<MindSystem>();
        var mind = minds.CreateMind(null);
        minds.TransferTo(mind, user);
        roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
        roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
        role!.Value.Comp2.Rule = rule;
        cult.Members.Add(mind);
        return user;
    }

    private EntityUid EquipTool(EntityUid user, EntityCoordinates coordinates)
    {
        var tool = SEntMan.SpawnEntity("OrbitraRatvarFabricator", coordinates);
        Server.System<SharedHandsSystem>().TryPickup(user, tool);
        return tool;
    }

    private void PrepareFloor(Entity<MapGridComponent> grid, Tile tile)
    {
        for (var x = -2; x <= 2; x++)
        for (var y = -2; y <= 2; y++)
            Server.System<SharedMapSystem>().SetTile(grid, new Vector2i(x, y), tile);
    }
}
