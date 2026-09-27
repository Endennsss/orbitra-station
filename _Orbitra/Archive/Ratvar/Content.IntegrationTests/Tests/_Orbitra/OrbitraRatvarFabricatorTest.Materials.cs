using System.Numerics;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed partial class OrbitraRatvarFabricatorTest
{
    private static readonly ProtoId<StackPrototype> BrassStack = "Brass";

    [TestCase(9, 0)]
    [TestCase(10, 1)]
    [TestCase(137, 13)]
    [TestCase(500, 50)]
    [TestCase(1000, 50)]
    public async Task BrassProductionChargesExactBatchAndRejectsDuplicate(int energy, int expected)
    {
        var map = await Pair.CreateTestMap();
        OrbitraRatvarRuleComponent cult = default!, otherCult = default!;
        EntityUid user = default, tool = default;
        var duplicate = true;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            Server.System<GameTicker>().StartGameRule(CultRule, out var otherRule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            otherCult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(otherRule);
            cult.Energy = energy;
            otherCult.Energy = 777;
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            JoinCult(user, rule);
            tool = SEntMan.SpawnEntity("OrbitraRatvarFabricator", map.GridCoords);
            Server.System<SharedHandsSystem>().TryPickup(user, tool);
            var use = new UseInHandEvent(user);
            SEntMan.EventBus.RaiseLocalEvent(tool, use);
            duplicate = Server.System<OrbitraRatvarFabricatorSystem>().TryProduceBrass(
                (tool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool)), user);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(duplicate, Is.False);
            Assert.That(cult.Energy, Is.EqualTo(energy - expected * 10));
            Assert.That(otherCult.Energy, Is.EqualTo(777));
            Assert.That(cult.Generated, Is.Zero, "Production must not advance generation progress.");
            Assert.That(CountBrass(), Is.EqualTo(expected));
        });

        if (energy != 1000)
            return;
        await Pair.RunSeconds(1.1f);
        var again = false;
        await Server.WaitPost(() => again = Server.System<OrbitraRatvarFabricatorSystem>().TryProduceBrass(
            (tool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool)), user));
        await Server.WaitAssertion(() =>
        {
            Assert.That(again, Is.True);
            Assert.That(cult.Energy, Is.Zero);
            Assert.That(CountBrass(), Is.EqualTo(100));
        });
    }

    [TestCase("crew")]
    [TestCase("drop")]
    [TestCase("mind-lost")]
    [TestCase("cult-end")]
    [TestCase("queued-tool")]
    [TestCase("busy")]
    [TestCase("zero-cost")]
    public async Task BrassProductionRejectsUnauthorizedOrBusyUse(string scenario)
    {
        var map = await Pair.CreateTestMap();
        OrbitraRatvarRuleComponent cult = default!;
        var produced = true;
        var busyStarted = false;
        await Server.WaitPost(() =>
        {
            AddFloor(map.Grid, map.Tile.Tile);
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = 1000;
            var user = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position - Vector2.UnitX));
            var mind = scenario == "crew" ? default : JoinCult(user, rule);
            var tool = SEntMan.SpawnEntity("OrbitraRatvarFabricator", map.GridCoords);
            Server.System<SharedHandsSystem>().TryPickup(user, tool);
            var component = SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool);
            var fabricator = Server.System<OrbitraRatvarFabricatorSystem>();
            switch (scenario)
            {
                case "drop": Server.System<SharedHandsSystem>().TryDrop(user, tool); break;
                case "mind-lost": Server.System<MindSystem>().TransferTo(mind, null); break;
                case "cult-end": Server.System<GameTicker>().EndGameRule(rule); break;
                case "queued-tool": SEntMan.QueueDeleteEntity(tool); break;
                case "zero-cost": component.BrassEnergy = 0; break;
                case "busy":
                    var target = SEntMan.SpawnEntity("OrbitraRatvarDoor", map.GridCoords);
                    Damage(target, 60);
                    busyStarted = fabricator.TryStartRepair((tool, component), user, target);
                    break;
            }
            produced = fabricator.TryProduceBrass((tool, component), user);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(produced, Is.False);
            if (scenario == "busy")
                Assert.That(busyStarted, Is.True);
            Assert.That(cult.Energy, Is.EqualTo(1000));
            Assert.That(CountBrass(), Is.Zero);
        });
    }

    private int CountBrass()
    {
        var count = 0;
        var query = SEntMan.EntityQueryEnumerator<StackComponent>();
        while (query.MoveNext(out var stack))
        {
            if (stack.StackTypeId == BrassStack)
                count += stack.Count;
        }
        return count;
    }
}
