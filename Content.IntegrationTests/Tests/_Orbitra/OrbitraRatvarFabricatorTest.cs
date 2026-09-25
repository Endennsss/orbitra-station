using Robust.Shared.Prototypes;
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraRatvarFabricatorTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";
    private static readonly ProtoId<DamageTypePrototype> BluntPrototype = "Blunt";

    // Общий набор тестовых прототипов сейчас падает в загрузчике до запуска сценариев.
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [TestCase("normal", true)]
    [TestCase("drop", false)]
    [TestCase("mind-return", false)]
    [TestCase("delete-target", false)]
    [TestCase("delete-tool", false)]
    [TestCase("energy", false)]
    [TestCase("cult-end", false)]
    [TestCase("damage", false)]
    [TestCase("foreign", false)]
    public async Task RepairRevalidatesAuthority(string scenario, bool expected)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, tool = default, target = default, mind = default, rule = default;
        OrbitraRatvarRuleComponent cult = default!;
        var started = false;
        var duplicate = true;
        await Server.WaitPost(() =>
        {
            AddFloor(map.Grid, map.Tile.Tile);
            Server.System<GameTicker>().StartGameRule(CultRule, out rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = 400;
            user = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position - Vector2.UnitX));
            mind = JoinCult(user, rule);
            tool = SEntMan.SpawnEntity("OrbitraRatvarFabricator", map.GridCoords);
            target = SEntMan.SpawnEntity("OrbitraRatvarDoor", map.GridCoords);
            Damage(target, 60);
            Server.System<SharedHandsSystem>().TryPickup(user, tool);
            var interaction = new AfterInteractEvent(user, tool, target, map.GridCoords, true);
            SEntMan.EventBus.RaiseLocalEvent(tool, interaction);
            started = SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool).Pending != null;
            duplicate = Server.System<OrbitraRatvarFabricatorSystem>().TryStartRepair(
                (tool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool)), user, target);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(duplicate, Is.False);
            Assert.That(cult.Energy, Is.EqualTo(400));
        });
        await Pair.RunSeconds(1);
        await Server.WaitPost(() =>
        {
            switch (scenario)
            {
                case "drop": Server.System<SharedHandsSystem>().TryDrop(user, tool); break;
                case "mind-return": Server.System<MindSystem>().TransferTo(mind, null); break;
                case "delete-target": SEntMan.DeleteEntity(target); break;
                case "delete-tool": SEntMan.DeleteEntity(tool); break;
                case "energy": cult.Energy = 199; break;
                case "cult-end": Server.System<GameTicker>().EndGameRule(rule); break;
                case "damage": Damage(user, 10); break;
                case "foreign":
                    Server.System<GameTicker>().StartGameRule(CultRule, out var other);
                    SEntMan.AddComponent<OrbitraRatvarStructureComponent>(target).Rule = other;
                    break;
            }
        });
        if (scenario == "mind-return")
        {
            await Pair.RunTicksSync(2);
            await Server.WaitPost(() => Server.System<MindSystem>().TransferTo(mind, user));
        }
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(cult.Energy, Is.EqualTo(scenario == "energy" ? 199 : expected ? 200 : 400));
            if (SEntMan.EntityExists(target))
                Assert.That(Server.System<DamageableSystem>().GetTotalDamage(target).Float(), Is.EqualTo(expected ? 5 : 60));
            if (SEntMan.EntityExists(tool))
                Assert.That(SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool).Pending, Is.Null);
        });
    }

    [TestCase("WallSolid", false)]
    [TestCase("WallBrass", true)]
    [TestCase("OrbitraRatvarWindow", true)]
    [TestCase("OrbitraRatvarBarricade", true)]
    [TestCase("OrbitraRatvarObelisk", true)]
    public async Task OnlySupportedStructuresCanBeRepaired(string prototype, bool expected)
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        OrbitraRatvarRuleComponent cult = default!;
        var started = false;
        await Server.WaitPost(() =>
        {
            AddFloor(map.Grid, map.Tile.Tile);
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = 400;
            var user = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position - Vector2.UnitX));
            JoinCult(user, rule);
            target = SEntMan.SpawnEntity(prototype, map.GridCoords);
            if (SEntMan.TryGetComponent<OrbitraRatvarStructureComponent>(target, out var structure))
                structure.Rule = rule;
            Damage(target, 20);
            var tool = SEntMan.SpawnEntity("OrbitraRatvarFabricator", map.GridCoords);
            Server.System<SharedHandsSystem>().TryPickup(user, tool);
            started = Server.System<OrbitraRatvarFabricatorSystem>().TryStartRepair(
                (tool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool)), user, target);
        });
        await Pair.RunSeconds(7);
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.EqualTo(expected));
            Assert.That(cult.Energy, Is.EqualTo(expected ? 200 : 400));
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(target).Float(), Is.EqualTo(expected ? 0 : 20));
        });
    }

    [Test]
    public async Task ConcurrentRepairsCannotOverspend()
    {
        var map = await Pair.CreateTestMap();
        OrbitraRatvarRuleComponent cult = default!;
        var targets = new List<EntityUid>();
        var started = new List<bool>();
        await Server.WaitPost(() =>
        {
            AddFloor(map.Grid, map.Tile.Tile);
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = 200;
            for (var i = 0; i < 2; i++)
            {
                var position = new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + new Vector2(0, i * 2));
                var user = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(position.EntityId, position.Position - Vector2.UnitX));
                JoinCult(user, rule);
                var target = SEntMan.SpawnEntity("OrbitraRatvarDoor", position);
                targets.Add(target);
                Damage(target, 60);
                var tool = SEntMan.SpawnEntity("OrbitraRatvarFabricator", map.GridCoords);
                Server.System<SharedHandsSystem>().TryPickup(user, tool);
                started.Add(Server.System<OrbitraRatvarFabricatorSystem>().TryStartRepair(
                    (tool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool)), user, target));
            }
        });
        await Pair.RunSeconds(7);
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.All.True);
            Assert.That(cult.Energy, Is.Zero);
            var damage = Server.System<DamageableSystem>();
            Assert.That(damage.GetTotalDamage(targets[0]).Float() + damage.GetTotalDamage(targets[1]).Float(), Is.EqualTo(65));
        });
    }

    [TestCase("crew")]
    [TestCase("healthy")]
    [TestCase("poor")]
    [TestCase("not-held")]
    public async Task InvalidRepairDoesNotStartOrCharge(string scenario)
    {
        var map = await Pair.CreateTestMap();
        OrbitraRatvarRuleComponent cult = default!;
        var started = true;
        await Server.WaitPost(() =>
        {
            AddFloor(map.Grid, map.Tile.Tile);
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = scenario == "poor" ? 199 : 400;
            var user = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position - Vector2.UnitX));
            if (scenario != "crew")
                JoinCult(user, rule);
            var target = SEntMan.SpawnEntity("OrbitraRatvarDoor", map.GridCoords);
            if (scenario != "healthy")
                Damage(target, 60);
            var tool = SEntMan.SpawnEntity("OrbitraRatvarFabricator", map.GridCoords);
            if (scenario != "not-held")
                Server.System<SharedHandsSystem>().TryPickup(user, tool);
            started = Server.System<OrbitraRatvarFabricatorSystem>().TryStartRepair(
                (tool, SEntMan.GetComponent<OrbitraRatvarFabricatorComponent>(tool)), user, target);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.False);
            Assert.That(cult.Energy, Is.EqualTo(scenario == "poor" ? 199 : 400));
        });
    }

    private EntityUid JoinCult(EntityUid user, EntityUid rule)
    {
        var mind = Server.System<MindSystem>().CreateMind(null);
        Server.System<MindSystem>().TransferTo(mind, user);
        var roles = Server.System<RoleSystem>();
        roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
        roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
        role!.Value.Comp2.Rule = rule;
        SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Members.Add(mind);
        return mind;
    }

    private void Damage(EntityUid target, int amount) => Server.System<DamageableSystem>().TryChangeDamage(
        target, new DamageSpecifier(SProtoMan.Index(BluntPrototype), amount), ignoreResistances: true);

    private void AddFloor(Entity<MapGridComponent> grid, Tile tile)
    {
        var maps = Server.System<SharedMapSystem>();
        for (var x = -2; x <= 2; x++)
        for (var y = -2; y <= 3; y++)
            maps.SetTile(grid, new Robust.Shared.Maths.Vector2i(x, y), tile);
    }
}
