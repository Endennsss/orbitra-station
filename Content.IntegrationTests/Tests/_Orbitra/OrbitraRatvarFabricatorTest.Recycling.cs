using System.Numerics;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed partial class OrbitraRatvarFabricatorTest
{
    [TestCase("SheetSteel10", 10, 5)]
    [TestCase("SheetGlass10", 5, 2)]
    [TestCase("SheetSteel1", 1, 0)]
    [TestCase("SheetPlasteel", 10, 0)]
    [TestCase("SheetBrass10", 10, 0)]
    public async Task RecyclingConservesWholePairs(string prototype, int sourceCount, int expected)
    {
        var map = await Pair.CreateTestMap();
        EntityUid source = default;
        OrbitraRatvarRuleComponent cult = default!;
        var duplicate = true;
        var production = true;
        var brassBefore = 0;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = 1000;
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            JoinCult(user, rule);
            var tool = SEntMan.SpawnEntity("OrbitraRatvarFabricator", map.GridCoords);
            Server.System<SharedHandsSystem>().TryPickup(user, tool);
            source = SEntMan.SpawnEntity(prototype, map.GridCoords);
            Server.System<SharedStackSystem>().SetCount(source, sourceCount);
            brassBefore = CountBrass();
            var interaction = new AfterInteractEvent(user, tool, source, map.GridCoords, true);
            SEntMan.EventBus.RaiseLocalEvent(tool, interaction);
            var fabricator = Server.System<OrbitraRatvarFabricatorSystem>();
            var component = SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool);
            duplicate = fabricator.TryRecycleSheets((tool, component), user, source);
            if (expected > 0)
                production = fabricator.TryProduceBrass((tool, component), user);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(duplicate, Is.False);
            if (expected > 0)
                Assert.That(production, Is.False, "Production shares recycling's cooldown.");
            Assert.That(CountBrass() - brassBefore, Is.EqualTo(expected));
            Assert.That(cult.Energy, Is.EqualTo(1000));
            Assert.That(cult.Generated, Is.Zero);
            var remainder = sourceCount - expected * 2;
            Assert.That(SEntMan.EntityExists(source), Is.EqualTo(remainder > 0));
            if (remainder > 0)
                Assert.That(SEntMan.GetComponent<StackComponent>(source).Count, Is.EqualTo(remainder));
        });
    }

    [TestCase("crew")]
    [TestCase("drop")]
    [TestCase("unlimited")]
    [TestCase("contained")]
    [TestCase("contents")]
    [TestCase("queued")]
    [TestCase("distant")]
    [TestCase("busy")]
    public async Task RecyclingRejectsUnsafeSources(string scenario)
    {
        var map = await Pair.CreateTestMap();
        EntityUid source = default, item = default;
        var accepted = true;
        var startedRepair = false;
        await Server.WaitPost(() =>
        {
            AddFloor(map.Grid, map.Tile.Tile);
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Energy = 1000;
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords.Offset(-Vector2.UnitX));
            if (scenario != "crew")
                JoinCult(user, rule);
            var tool = SEntMan.SpawnEntity("OrbitraRatvarFabricator", map.GridCoords);
            Server.System<SharedHandsSystem>().TryPickup(user, tool);
            source = SEntMan.SpawnEntity("SheetSteel10", scenario == "distant" ? map.GridCoords.Offset(Vector2.One * 10) : map.GridCoords);
            var component = SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool);
            var fabricator = Server.System<OrbitraRatvarFabricatorSystem>();
            switch (scenario)
            {
                case "drop": Server.System<SharedHandsSystem>().TryDrop(user, tool); break;
                case "unlimited":
                    // Моделируем бесконечный ресурс без изменения рабочего прототипа.
#pragma warning disable RA0002
                    SEntMan.GetComponent<StackComponent>(source).Unlimited = true;
#pragma warning restore RA0002
                    break;
                case "contained":
                    var box = SEntMan.SpawnEntity(null, map.GridCoords);
                    var containers = Server.System<SharedContainerSystem>();
                    containers.Insert(source, containers.EnsureContainer<ContainerSlot>(box, "source"));
                    break;
                case "contents":
                    item = SEntMan.SpawnEntity("Crowbar", map.GridCoords);
                    var storage = Server.System<SharedContainerSystem>();
                    storage.Insert(item, storage.EnsureContainer<ContainerSlot>(source, "contents"));
                    break;
                case "queued": SEntMan.QueueDeleteEntity(source); break;
                case "busy":
                    var target = SEntMan.SpawnEntity("OrbitraRatvarDoor", map.GridCoords);
                    Damage(target, 60);
                    startedRepair = fabricator.TryStartRepair((tool, component), user, target);
                    break;
            }
            accepted = fabricator.TryRecycleSheets((tool, component), user, source);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(accepted, Is.False);
            Assert.That(CountBrass(), Is.Zero);
            if (scenario == "busy")
                Assert.That(startedRepair, Is.True);
            if (scenario != "queued")
                Assert.That(SEntMan.GetComponent<StackComponent>(source).Count, Is.EqualTo(10));
            if (scenario == "contents")
                Assert.That(SEntMan.EntityExists(item), Is.True);
        });
    }

    [Test]
    public async Task RecyclingTwoCultToolsCannotConsumeTheSameSheetsTwice()
    {
        var map = await Pair.CreateTestMap();
        var first = false;
        var second = true;
        OrbitraRatvarRuleComponent firstCult = default!, secondCult = default!;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            Server.System<GameTicker>().StartGameRule(CultRule, out var other);
            firstCult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            secondCult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(other);
            firstCult.Energy = 0;
            secondCult.Energy = 777;
            var source = SEntMan.SpawnEntity("SheetSteel10", map.GridCoords);
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var otherUser = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            JoinCult(user, rule);
            JoinCult(otherUser, other);
            var tool = SEntMan.SpawnEntity("OrbitraRatvarFabricator", map.GridCoords);
            var otherTool = SEntMan.SpawnEntity("OrbitraRatvarFabricator", map.GridCoords);
            Server.System<SharedHandsSystem>().TryPickup(user, tool);
            Server.System<SharedHandsSystem>().TryPickup(otherUser, otherTool);
            var fabricator = Server.System<OrbitraRatvarFabricatorSystem>();
            first = fabricator.TryRecycleSheets((tool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool)), user, source);
            second = fabricator.TryRecycleSheets((otherTool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(otherTool)), otherUser, source);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(CountBrass(), Is.EqualTo(5));
            Assert.That(firstCult.Energy, Is.Zero);
            Assert.That(secondCult.Energy, Is.EqualTo(777));
        });
    }
}
