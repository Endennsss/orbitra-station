using System.Numerics;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed partial class OrbitraRatvarFloorTest
{
    [Category("OrbitraRatvarExpansion")]
    [TestCase("active", true)]
    [TestCase("unbound", false)]
    [TestCase("lost", false)]
    [TestCase("won", false)]
    [TestCase("ended", false)]
    public async Task SlowingSigilRequiresActiveOwner(string state, bool expected)
    {
        var map = await Pair.CreateTestMap();
        var triggered = false;
        await Server.WaitPost(() =>
        {
            PrepareFloor(map.Grid, map.Tile.Tile);
            var center = map.GridCoords.Offset(new Vector2(0.5f));
            var user = CreateCultist(center.Offset(-Vector2.UnitX), out var cult);
            var system = Server.System<OrbitraRatvarRuleSystem>();
            system.TryGetCult(user, out var rule);
            var target = SEntMan.SpawnEntity("MobHuman", center);
            var sigil = SEntMan.SpawnEntity("OrbitraRatvarSlowingSigil", center);
            var structure = SEntMan.GetComponent<OrbitraRatvarStructureComponent>(sigil);
            if (state != "unbound")
                structure.Rule = rule.Owner;
            cult.Won = state == "won";
            cult.Lost = state == "lost";
            if (state == "ended")
                Server.System<GameTicker>().EndGameRule(rule.Owner);
            triggered = system.TryTriggerSlowingSigil((sigil, structure), target);
        });
        await Server.WaitAssertion(() => Assert.That(triggered, Is.EqualTo(expected)));
    }
}
