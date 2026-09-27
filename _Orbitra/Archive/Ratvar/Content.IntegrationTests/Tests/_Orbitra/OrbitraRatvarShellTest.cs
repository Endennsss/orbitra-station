using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.NPC.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraRatvarShellTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";
    private static readonly ProtoId<OrbitraRatvarScripturePrototype> MarauderScripture = "OrbitraRatvarMarauder";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task GhostTakeoverRespectsShellBinding(bool bound, bool ended)
    {
        var map = await Pair.CreateTestMap();
        EntityUid shell = default;
        EntityUid rule = default;
        EntityUid mind = default;
        EntityUid replacement = default;
        var tookRole = false;
        var joined = bound && !ended;
        await Server.WaitPost(() =>
        {
            shell = SEntMan.SpawnEntity("OrbitraRatvarMarauder", map.GridCoords);
            replacement = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            if (bound)
            {
                Server.System<GameTicker>().StartGameRule(CultRule, out rule);
                SEntMan.GetComponent<OrbitraRatvarShellComponent>(shell).Rule = rule;
                if (ended)
                    Server.System<GameTicker>().EndGameRule(rule);
            }
        });
        await Pair.RunTicksSync(2);
        await Server.WaitPost(() =>
        {
            var ghostRole = SEntMan.GetComponent<GhostRoleComponent>(shell);
            tookRole = Server.System<GhostRoleSystem>().Takeover(ServerSession!, ghostRole.Identifier);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(tookRole, Is.True);
            Assert.That(ServerSession!.AttachedEntity, Is.EqualTo(shell));
            Assert.That(Server.System<MindSystem>().TryGetMind(shell, out mind, out _), Is.True);
            Assert.That(Server.System<RoleSystem>().MindHasRole<OrbitraRatvarRoleComponent>(mind), Is.EqualTo(joined));
            Assert.That(Server.System<OrbitraRatvarRuleSystem>().TryGetCult(shell, out var cult), Is.EqualTo(joined));
            if (joined)
            {
                Assert.That(cult.Owner, Is.EqualTo(rule));
                Assert.That(cult.Comp.Members, Is.EquivalentTo(new[] { mind }));
                Assert.That(cult.Comp.Converted, Is.Empty, "Construct occupation must not award conversion progress.");
            }
            Assert.That(SEntMan.EntityQuery<OrbitraRatvarRuleComponent>().Count(), Is.EqualTo(bound ? 1 : 0));
        });

        await Server.WaitPost(() => Server.System<MindSystem>().TransferTo(mind, replacement));
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<OrbitraRatvarRuleSystem>().TryGetCult(shell, out _), Is.False);
            Assert.That(SEntMan.HasComponent<OrbitraRatvarBodyComponent>(shell), Is.False);
            Assert.That(SEntMan.GetComponent<NpcFactionMemberComponent>(shell).Factions,
                Does.Not.Contain("OrbitraRatvar"));
            Assert.That(Server.System<OrbitraRatvarRuleSystem>().TryGetCult(replacement, out _), Is.EqualTo(joined));
        });
        await Server.WaitPost(() => Server.System<MindSystem>().TransferTo(mind, shell));
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<OrbitraRatvarRuleSystem>().TryGetCult(shell, out _), Is.EqualTo(joined));
            Assert.That(SEntMan.HasComponent<OrbitraRatvarBodyComponent>(replacement), Is.False);
            if (joined)
                Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Members,
                    Is.EquivalentTo(new[] { mind }), "Returning to a shell must not duplicate membership.");
        });
    }

    [Test]
    public async Task ShellReservationsAreIsolatedAndReleasedOnDestruction()
    {
        var map = await Pair.CreateTestMap();
        var rules = new EntityUid[2];
        var creators = new EntityUid[2];
        var tablets = new Entity<OrbitraRatvarTabletComponent>[2];
        var purchases = new bool[2, 3];
        var remainingEnergy = new int[2];
        await Server.WaitPost(() =>
        {
            // Админские тела не должны отнимать места у создаваемых писанием оболочек.
            for (var i = 0; i < 3; i++)
                SEntMan.SpawnEntity("OrbitraRatvarMarauder", map.GridCoords);
            for (var i = 0; i < rules.Length; i++)
            {
                Server.System<GameTicker>().StartGameRule(CultRule, out rules[i]);
                var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rules[i]);
                cult.Generated = cult.TierThreeEnergy;
                for (var converted = 0; converted < cult.TierThreeConverts; converted++)
                    cult.Converted.Add(Server.System<MindSystem>().CreateMind(null));
                cult.Energy = cult.MaxEnergy;
                creators[i] = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                var mind = Server.System<MindSystem>().CreateMind(null);
                Server.System<MindSystem>().TransferTo(mind, creators[i]);
                var roles = Server.System<RoleSystem>();
                roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
                roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
                role!.Value.Comp2.Rule = rules[i];
                cult.Members.Add(mind);
                var tablet = SEntMan.SpawnEntity("OrbitraRatvarTablet", map.GridCoords);
                Server.System<SharedHandsSystem>().TryPickup(creators[i], tablet);
                tablets[i] = (tablet, SEntMan.GetComponent<OrbitraRatvarTabletComponent>(tablet));
                for (var purchase = 0; purchase < 3; purchase++)
                    purchases[i, purchase] = Server.System<OrbitraRatvarRuleSystem>()
                        .TryCompleteScripture(tablets[i], creators[i], "OrbitraRatvarMarauder");
                remainingEnergy[i] = cult.Energy;
            }
        });
        await Server.WaitAssertion(() =>
        {
            for (var i = 0; i < rules.Length; i++)
            {
                Assert.That(purchases[i, 0], Is.True);
                Assert.That(purchases[i, 1], Is.True);
                Assert.That(purchases[i, 2], Is.False);
                var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rules[i]);
                var scripture = SProtoMan.Index(MarauderScripture);
                Assert.That(remainingEnergy[i], Is.EqualTo(cult.MaxEnergy - 2 * scripture.Energy));
                Assert.That(SEntMan.EntityQuery<OrbitraRatvarShellComponent>().Count(s => s.Rule == rules[i]), Is.EqualTo(2));
            }
        });
        await Server.WaitPost(() =>
        {
            var destroyed = SEntMan.EntityQueryEnumerator<OrbitraRatvarShellComponent>();
            while (destroyed.MoveNext(out var uid, out var shell))
            {
                if (shell.Rule != rules[0])
                    continue;
                SEntMan.DeleteEntity(uid);
                break;
            }
            for (var i = 0; i < rules.Length; i++)
                purchases[i, 0] = Server.System<OrbitraRatvarRuleSystem>()
                    .TryCompleteScripture(tablets[i], creators[i], "OrbitraRatvarMarauder");
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(purchases[0, 0], Is.True, "Destroying a shell releases its reservation.");
            Assert.That(purchases[1, 0], Is.False, "Another cult must retain its own full limit.");
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rules[1]).Energy, Is.EqualTo(remainingEnergy[1]));
        });
    }

    [Test]
    public async Task EndingCultRemovesBodyAllegiance()
    {
        var map = await Pair.CreateTestMap();
        EntityUid shell = default;
        EntityUid rule = default;
        EntityUid otherShell = default;
        EntityUid otherRule = default;
        EntityUid mind = default;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(CultRule, out rule);
            shell = SEntMan.SpawnEntity("OrbitraRatvarMarauder", map.GridCoords);
            SEntMan.GetComponent<OrbitraRatvarShellComponent>(shell).Rule = rule;
            mind = Server.System<MindSystem>().CreateMind(ServerSession!.UserId);
            Server.System<MindSystem>().TransferTo(mind, shell);
            Server.System<GameTicker>().StartGameRule(CultRule, out otherRule);
            otherShell = SEntMan.SpawnEntity("OrbitraRatvarMarauder", map.GridCoords);
            SEntMan.GetComponent<OrbitraRatvarShellComponent>(otherShell).Rule = otherRule;
            Server.System<MindSystem>().TransferTo(Server.System<MindSystem>().CreateMind(null), otherShell);
        });
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<OrbitraRatvarBodyComponent>(shell), Is.True));
        await Server.WaitPost(() => Server.System<GameTicker>().EndGameRule(rule));
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<OrbitraRatvarRuleSystem>().TryGetCult(shell, out _), Is.False);
            Assert.That(SEntMan.HasComponent<OrbitraRatvarBodyComponent>(shell), Is.False);
            Assert.That(SEntMan.GetComponent<NpcFactionMemberComponent>(shell).Factions, Does.Not.Contain("OrbitraRatvar"));
            Assert.That(SEntMan.HasComponent<OrbitraRatvarMarauderComponent>(shell), Is.True,
                "Ending membership must not remove innate body abilities.");
            Assert.That(Server.System<RoleSystem>().MindHasRole<OrbitraRatvarRoleComponent>(mind), Is.True,
                "Mind roles remain available for round-end reporting.");
            Assert.That(Server.System<OrbitraRatvarRuleSystem>().TryGetCult(otherShell, out var otherCult), Is.True);
            Assert.That(otherCult.Owner, Is.EqualTo(otherRule));
            Assert.That(SEntMan.HasComponent<OrbitraRatvarBodyComponent>(otherShell), Is.True);
            Assert.That(SEntMan.GetComponent<NpcFactionMemberComponent>(otherShell).Factions, Does.Contain("OrbitraRatvar"));
        });
    }
}
