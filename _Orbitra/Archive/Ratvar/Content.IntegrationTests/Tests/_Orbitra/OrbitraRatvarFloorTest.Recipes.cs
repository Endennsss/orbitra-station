using System.Collections.Generic;
using System.Numerics;
using Content.Server._Orbitra.Ratvar;
using Content.Shared.DoAfter;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed partial class OrbitraRatvarFloorTest
{
    [Test, Category("OrbitraRatvarExpansion")]
    public async Task EveryDeclaredAirlockRecipeCanStartWithoutPayment()
    {
        var map = await Pair.CreateTestMap();
        var rejected = new List<string>();
        OrbitraRatvarRuleComponent cult = null!;
        await Server.WaitPost(() =>
        {
            PrepareFloor(map.Grid, map.Tile.Tile);
            var center = map.GridCoords.Offset(new Vector2(0.5f));
            var user = CreateCultist(center.Offset(-Vector2.UnitX), out cult);
            cult.Energy = 400;
            var tool = EquipTool(user, center);
            var settings = SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool);
            var system = Server.System<OrbitraRatvarFabricatorSystem>();
            foreach (var prototype in settings.Airlocks.Keys)
            {
                var door = SEntMan.SpawnEntity(prototype, center);
                if (!system.TryStartDoor((tool, settings), user, door))
                    rejected.Add(prototype.Id);
                if (settings.Pending is { } pending)
                    Server.System<SharedDoAfterSystem>().Cancel(pending);
                SEntMan.DeleteEntity(door);
            }
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(rejected, Is.Empty, "Every listed station airlock must pass the actual conversion validator.");
            Assert.That(cult.Energy, Is.EqualTo(400));
        });
    }
}
