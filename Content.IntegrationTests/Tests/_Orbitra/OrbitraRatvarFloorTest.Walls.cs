using System.Numerics;
using System.Linq;
using Content.Server._Orbitra.Ratvar;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed partial class OrbitraRatvarFloorTest
{
    private static readonly ProtoId<DamageTypePrototype> WallTestBlunt = "Blunt";

    [TestCase(false)]
    [TestCase(true)]
    public async Task WallConcurrentConversionChargesOnlyOnce(bool window)
    {
        var map = await Pair.CreateTestMap();
        OrbitraRatvarRuleComponent cult = null!;
        var firstStarted = false;
        var secondStarted = false;
        await Server.WaitPost(() =>
        {
            PrepareFloor(map.Grid, map.Tile.Tile);
            var center = map.GridCoords.Offset(new Vector2(0.5f));
            var first = CreateCultist(center.Offset(-Vector2.UnitX), out cult);
            var second = CreateCultist(center.Offset(Vector2.UnitY), out _, cult.Members.First());
            cult.Energy = 400;
            var wall = SEntMan.SpawnEntity(window ? "ReinforcedWindow" : "WallSolid", center);
            var firstTool = EquipTool(first, center);
            var secondTool = EquipTool(second, center);
            var fabricator = Server.System<OrbitraRatvarFabricatorSystem>();
            firstStarted = window
                ? fabricator.TryStartWindow((firstTool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(firstTool)), first, wall)
                : fabricator.TryStartWall((firstTool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(firstTool)), first, wall);
            secondStarted = window
                ? fabricator.TryStartWindow((secondTool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(secondTool)), second, wall)
                : fabricator.TryStartWall((secondTool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(secondTool)), second, wall);
        });
        await Server.WaitAssertion(() => Assert.That(firstStarted && secondStarted, Is.True));
        await Pair.RunSeconds(7);
        await Server.WaitAssertion(() => Assert.That(cult.Energy, Is.EqualTo(200)));
    }

    [TestCase("WallPlastitaniumIndestructible")]
    [TestCase("WallSolidDiagonal")]
    [TestCase("ReinforcedWindowDiagonal", true)]
    public async Task WallUnsupportedPrototypeCannotStart(string prototype, bool window = false)
    {
        var map = await Pair.CreateTestMap();
        OrbitraRatvarRuleComponent cult = null!;
        var started = true;
        await Server.WaitPost(() =>
        {
            PrepareFloor(map.Grid, map.Tile.Tile);
            var center = map.GridCoords.Offset(new Vector2(0.5f));
            var user = CreateCultist(center.Offset(-Vector2.UnitX), out cult);
            cult.Energy = 400;
            var tool = EquipTool(user, center);
            var wall = SEntMan.SpawnEntity(prototype, center);
            var fabricator = Server.System<OrbitraRatvarFabricatorSystem>();
            var ent = (tool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool));
            started = window ? fabricator.TryStartWindow(ent, user, wall) : fabricator.TryStartWall(ent, user, wall);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.False);
            Assert.That(cult.Energy, Is.EqualTo(400));
        });
    }

    [TestCase("success")]
    [TestCase("success", false, "WallReinforced", Category = "OrbitraRatvarFeedback")]
    [TestCase("success", false, "WallSolidRust", Category = "OrbitraRatvarFeedback")]
    [TestCase("success", false, "WallShuttle", Category = "OrbitraRatvarFeedback")]
    [TestCase("contents")]
    [TestCase("child")]
    [TestCase("damage")]
    [TestCase("delete")]
    [TestCase("drop")]
    [TestCase("success", true)]
    [TestCase("success", true, "Window", Category = "OrbitraRatvarExpansion")]
    [TestCase("success", true, "TintedWindow", Category = "OrbitraRatvarExpansion")]
    [TestCase("contents", true)]
    [TestCase("child", true)]
    [TestCase("damage", true)]
    [TestCase("delete", true)]
    [TestCase("drop", true)]
    public async Task WallConversionPreservesInfrastructureOrCancels(string scenario, bool window = false, string source = null)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, tool = default, wall = default, pipe = default, cable = default, item = default;
        OrbitraRatvarRuleComponent cult = null!;
        var started = false;
        await Server.WaitPost(() =>
        {
            PrepareFloor(map.Grid, map.Tile.Tile);
            var center = map.GridCoords.Offset(new Vector2(0.5f));
            user = CreateCultist(center.Offset(-Vector2.UnitX), out cult);
            cult.Energy = 400;
            tool = EquipTool(user, center);
            wall = SEntMan.SpawnEntity(source ?? (window ? "ReinforcedWindow" : "WallSolid"), center);
            pipe = SEntMan.SpawnEntity("GasPipeStraight", center);
            cable = SEntMan.SpawnEntity("CableHV", center);
            item = SEntMan.SpawnEntity("Crowbar", center);
            var interaction = new AfterInteractEvent(user, tool, wall, center, true);
            SEntMan.EventBus.RaiseLocalEvent(tool, interaction);
            started = SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool).Pending != null;
        });
        await Server.WaitAssertion(() => Assert.That(started, Is.True));
        await Pair.RunSeconds(0.5f);
        await Server.WaitPost(() =>
        {
            switch (scenario)
            {
                case "contents":
                    var containers = Server.System<SharedContainerSystem>();
                    containers.Insert(item, containers.EnsureContainer<ContainerSlot>(wall, "wall-test"));
                    break;
                case "child":
                    Server.System<SharedTransformSystem>().SetCoordinates(item, new Robust.Shared.Map.EntityCoordinates(wall, Vector2.Zero));
                    break;
                case "damage":
                    Server.System<DamageableSystem>().TryChangeDamage(wall,
                        new DamageSpecifier(SProtoMan.Index(WallTestBlunt), 1), ignoreResistances: true);
                    break;
                case "delete": SEntMan.DeleteEntity(wall); break;
                case "drop": Server.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>().TryDrop(user, tool); break;
            }
        });
        await Pair.RunSeconds(7);
        await Server.WaitAssertion(() =>
        {
            Assert.That(cult.Energy, Is.EqualTo(scenario == "success" ? 200 : 400));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool).Pending, Is.Null);
            Assert.That(SEntMan.EntityExists(wall), Is.EqualTo(scenario != "success" && scenario != "delete"));
            foreach (var entity in new[] { pipe, cable, item })
                Assert.That(SEntMan.EntityExists(entity), Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(pipe).Anchored, Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(cable).Anchored, Is.True);
            var brass = 0;
            foreach (var entity in Server.System<SharedMapSystem>().GetAnchoredEntities(map.Grid, map.Grid.Comp, map.Tile.GridIndices))
            {
                if (SEntMan.GetComponent<MetaDataComponent>(entity).EntityPrototype?.ID == (window ? "OrbitraRatvarWindow" : "WallBrass"))
                {
                    brass++;
                    var construction = SEntMan.GetComponent<Content.Server.Construction.Components.ConstructionComponent>(entity);
                    Assert.That(construction.Graph.Id, Is.EqualTo(window ? "OrbitraRatvarWindow" : "Girder"));
                    Assert.That(construction.Node, Is.EqualTo(window ? "window" : "brassWall"));
                    Assert.That(Server.System<DamageableSystem>().GetTotalDamage(entity).Float(), Is.Zero);
                }
            }
            Assert.That(brass, Is.EqualTo(scenario == "success" ? 1 : 0));
            Assert.That(Server.System<AtmosphereSystem>().IsTileAirBlocked(map.Grid, map.Tile.GridIndices),
                Is.EqualTo(scenario != "delete"));
        });
    }
}
