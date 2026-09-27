using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._Orbitra.Ratvar;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared._Orbitra.ThermalVision;
using Content.Shared.Eye;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Buckle;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Station.Components;
using Content.Shared.Stealth.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Exercises three expansion blocks with live ownership and resource revalidation.</summary>
[TestFixture]
public sealed class OrbitraRatvarBlocksTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [Test]
    public async Task WindowsRetainButtonsAcrossStateRefresh()
    {
        await Client.WaitAssertion(() =>
        {
            using var cache = new OrbitraRatvarCacheWindow();
            cache.OpenCentered();
            var choices = new[] { "OrbitraRatvarRobes", "OrbitraRatvarCloak", "OrbitraRatvarSpectacles" };
            cache.Update(new OrbitraRatvarCacheUiState("orbitra-ratvar-cache-ready", 0, choices));
            var original = Descendants(cache).OfType<Button>().ToArray();
            cache.Update(new OrbitraRatvarCacheUiState("orbitra-ratvar-cache-cooldown", 240, choices));
            Assert.That(Descendants(cache).OfType<Button>(), Is.EqualTo(original));
            Assert.That(original.Count(b => b.Disabled), Is.GreaterThanOrEqualTo(3));
            cache.Measure(new Vector2(540, 420));
            cache.Arrange(UIBox2.FromDimensions(Vector2.Zero, cache.DesiredSize));
            Assert.That(cache.DesiredSize.X, Is.LessThanOrEqualTo(540));

            using var travel = new OrbitraRatvarTravelWindow();
            travel.OpenCentered();
            var points = new Dictionary<NetEntity, string> { [new NetEntity(777)] = "<literal> Station" };
            travel.Update(new OrbitraRatvarTravelUiState("Test", 2000, 5, "orbitra-ratvar-travel-ready", points));
            var buttons = Descendants(travel).OfType<Button>().ToArray();
            travel.Update(new OrbitraRatvarTravelUiState("Test", 1995, 5, "orbitra-ratvar-travel-ready", points));
            Assert.That(Descendants(travel).OfType<Button>(), Is.EqualTo(buttons));
            Assert.That(buttons.Any(b => b.Text == "<literal> Station"), Is.True);
            travel.Update(new OrbitraRatvarTravelUiState("Test", 1995, 5, "orbitra-ratvar-travel-ready", []));
            Assert.That(Descendants(travel).OfType<Button>().Any(b => b.Text == "<literal> Station"), Is.False);
        });
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    [TestCase("ready", true)]
    [TestCase("foreign", false)]
    [TestCase("unanchored", false)]
    [TestCase("empty", false)]
    [TestCase("deleted", false)]
    [TestCase("invalid-choice", false)]
    public async Task CacheChecksAuthorityAndSharesCooldown(string scenario, bool expected)
    {
        var map = await Pair.CreateTestMap();
        var success = false;
        var repeated = true;
        OrbitraRatvarCacheComponent cache = default!;
        await Server.WaitPost(() =>
        {
            PrepareFloor(map.Grid, map.Tile.Tile);
            var origin = map.GridCoords.Offset(new Vector2(0.5f));
            var (rule, user) = PrepareCult(origin);
            var machine = SEntMan.SpawnEntity("OrbitraRatvarCache", origin.Offset(Vector2.UnitX));
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(machine).Rule = rule;
            cache = SEntMan.GetComponent<OrbitraRatvarCacheComponent>(machine);
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            if (scenario == "foreign")
            {
                Server.System<GameTicker>().StartGameRule(CultRule, out var foreign);
                SEntMan.GetComponent<OrbitraRatvarStructureComponent>(machine).Rule = foreign;
            }
            if (scenario == "unanchored") Server.System<SharedTransformSystem>().Unanchor(machine);
            if (scenario == "empty") cult.Energy = 0;
            if (scenario == "deleted") SEntMan.QueueDeleteEntity(machine);
            var system = Server.System<OrbitraRatvarCacheSystem>();
            success = system.TryIssue((machine, cache), user, scenario == "invalid-choice" ? 100 : 0);
            repeated = system.TryIssue((machine, cache), user, 1);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(success, Is.EqualTo(expected));
            if (scenario != "invalid-choice") Assert.That(repeated, Is.False);
            Assert.That(cache.NextUse > TimeSpan.Zero, Is.EqualTo(expected || scenario == "invalid-choice"));
        });
    }

    [TestCase("ready", true)]
    [TestCase("wall", false)]
    [TestCase("occupied", false)]
    [TestCase("pulling", false)]
    [TestCase("pulled", false)]
    [TestCase("buckled", false)]
    [TestCase("foreign", false)]
    [TestCase("unanchored", false)]
    [TestCase("deleted", false)]
    public async Task TravelUsesExactAuthorizedDestination(string scenario, bool expected)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, source = default, target = default;
        OrbitraRatvarRuleComponent cult = default!;
        var success = false;
        var duplicate = true;
        await Server.WaitPost(() =>
        {
            PrepareFloor(map.Grid, map.Tile.Tile);
            var origin = map.GridCoords.Offset(new Vector2(0.5f));
            var (rule, body) = PrepareCult(origin);
            user = body;
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationDataComponent>(station);
            Server.System<StationSystem>().AddGridToStation(station, map.Grid);
            cult.Station = station;
            source = SpawnPoint(rule, origin.Offset(Vector2.UnitX));
            target = SpawnPoint(rule, origin.Offset(new Vector2(4, 0)));
            if (scenario == "wall") SEntMan.SpawnEntity("WallSolid", Transform(target).Coordinates);
            if (scenario == "occupied") SEntMan.SpawnEntity("MobHuman", Transform(target).Coordinates);
            if (scenario is "pulling" or "pulled")
            {
                var other = SEntMan.SpawnEntity("MobHuman", origin.Offset(new Vector2(0, 0.5f)));
                Assert.That(scenario == "pulling"
                    ? Server.System<PullingSystem>().TryStartPull(user, other)
                    : Server.System<PullingSystem>().TryStartPull(other, user), Is.True);
            }
            if (scenario == "buckled")
            {
                var chair = SEntMan.SpawnEntity("Chair", origin);
                Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(user, user, chair), Is.True);
            }
            if (scenario == "foreign")
            {
                Server.System<GameTicker>().StartGameRule(CultRule, out var foreign);
                SEntMan.GetComponent<OrbitraRatvarStructureComponent>(target).Rule = foreign;
            }
            if (scenario == "unanchored") Server.System<SharedTransformSystem>().Unanchor(target);
            if (scenario == "deleted") SEntMan.QueueDeleteEntity(target);
            var system = Server.System<OrbitraRatvarTravelSystem>();
            var component = SEntMan.GetComponent<OrbitraRatvarTravelComponent>(source);
            success = system.TryTravel((source, component), user, target);
            duplicate = system.TryTravel((source, component), user, target);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(success, Is.EqualTo(expected));
            Assert.That(duplicate, Is.False);
            Assert.That(cult.Energy, Is.EqualTo(expected ? 1995 : 2000));
            Assert.That(Transform(user).GridUid, Is.EqualTo(map.Grid.Owner));
            if (scenario != "buckled")
                Assert.That(Transform(user).LocalPosition.X, Is.EqualTo(expected ? 4.5f : 0.5f).Within(0.01f));
        });
    }

    [Test]
    public async Task DestroyingDestinationDuringRitualDoesNotSpendEnergy()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, target = default;
        OrbitraRatvarRuleComponent cult = default!;
        var started = false;
        await Server.WaitPost(() =>
        {
            PrepareFloor(map.Grid, map.Tile.Tile);
            var origin = map.GridCoords.Offset(new Vector2(0.5f));
            var (rule, body) = PrepareCult(origin);
            user = body;
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationDataComponent>(station);
            Server.System<StationSystem>().AddGridToStation(station, map.Grid);
            cult.Station = station;
            var source = SpawnPoint(rule, origin.Offset(Vector2.UnitX));
            target = SpawnPoint(rule, origin.Offset(new Vector2(4, 0)));
            started = Server.System<OrbitraRatvarTravelSystem>().TryStartTravel(
                (source, SEntMan.GetComponent<OrbitraRatvarTravelComponent>(source)), user, target);
        });
        await Server.WaitAssertion(() => Assert.That(started, Is.True));
        await Pair.RunSeconds(0.5f);
        await Server.WaitPost(() => SEntMan.DeleteEntity(target));
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(cult.Energy, Is.EqualTo(2000));
            Assert.That(Transform(user).LocalPosition.X, Is.EqualTo(0.5f).Within(0.1f));
        });
    }

    [Test]
    public async Task BuildersReserveSlotsAndAdminBodiesRemainIndependent()
    {
        var map = await Pair.CreateTestMap();
        EntityUid rule = default, first = default, admin = default;
        var results = new bool[3];
        await Server.WaitPost(() =>
        {
            var origin = map.GridCoords;
            (rule, var user) = PrepareCult(origin);
            var helper = SEntMan.SpawnEntity("MobHuman", origin);
            AddMember(helper, rule, false);
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.TierTwoConverts = 0;
            cult.TierTwoEnergy = 0;
            var tablet = SEntMan.SpawnEntity("OrbitraRatvarTablet", origin);
            Server.System<SharedHandsSystem>().TryPickupAnyHand(user, tablet);
            var system = Server.System<OrbitraRatvarRuleSystem>();
            for (var i = 0; i < results.Length; i++)
                results[i] = system.TryCompleteScripture((tablet, SEntMan.GetComponent<OrbitraRatvarTabletComponent>(tablet)), user, "OrbitraRatvarCogscarab");
            var shells = SEntMan.EntityQueryEnumerator<OrbitraRatvarShellComponent>();
            while (shells.MoveNext(out var shellUid, out var shell))
            {
                if (shell.Rule != rule) continue;
                first = shellUid;
                break;
            }
            admin = SEntMan.SpawnEntity("OrbitraRatvarCogscarab", origin);
            var minds = Server.System<MindSystem>();
            minds.TransferTo(minds.CreateMind(null), admin);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(results, Is.EqualTo(new[] { true, true, false }));
            var system = Server.System<OrbitraRatvarRuleSystem>();
            Assert.That(system.CountShells(rule, true), Is.EqualTo(2));
            Assert.That(system.CountShells(rule, false), Is.Zero);
            Assert.That(system.TryGetCult(admin, out _), Is.False);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Energy, Is.EqualTo(1000));
            Assert.That(Server.System<SharedHandsSystem>().EnumerateHeld(first).Count(), Is.EqualTo(2));
        });
        EntityUid mind = default;
        await Server.WaitPost(() =>
        {
            mind = Server.System<MindSystem>().CreateMind(null);
            Server.System<MindSystem>().TransferTo(mind, first);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<OrbitraRatvarRuleSystem>().TryGetCult(first, out var owner), Is.True);
            Assert.That(owner.Owner, Is.EqualTo(rule));
            Assert.That(Server.System<OrbitraRatvarRuleSystem>().CountShells(rule, true), Is.EqualTo(2));
        });
        await Server.WaitPost(() => SEntMan.DeleteEntity(first));
        await Server.WaitAssertion(() => Assert.That(Server.System<OrbitraRatvarRuleSystem>().CountShells(rule, true), Is.EqualTo(1)));
    }

    [TestCase("OrbitraRatvarCloak", "outerClothing")]
    [TestCase("OrbitraRatvarSpectacles", "eyes")]
    public async Task CacheEquipmentRevokesEffectsOnRemoval(string prototype, string slot)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, item = default;
        var equipped = false;
        await Server.WaitPost(() =>
        {
            (_, user) = PrepareCult(map.GridCoords);
            item = SEntMan.SpawnEntity(prototype, map.GridCoords);
            var equipment = SEntMan.GetComponent<OrbitraRatvarEquipmentComponent>(item);
            equipment.EyeDamageInterval = TimeSpan.FromSeconds(0.2);
            equipment.RecoveryDelay = TimeSpan.FromSeconds(0.5);
            equipped = Server.System<InventorySystem>().TryEquip(user, item, slot, force: true);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(equipped, Is.True);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEquipmentComponent>(item).Active, Is.True);
            if (slot == "eyes")
            {
                Assert.That(SEntMan.GetComponent<OrbitraThermalVisionComponent>(item).Enabled, Is.True);
                Assert.That(SEntMan.GetComponent<EyeComponent>(user).VisibilityMask & (int) VisibilityFlags.Ghost, Is.Not.Zero);
                Assert.That(SEntMan.GetComponent<BlindableComponent>(user).EyeDamage, Is.Positive);
            }
            else Assert.That(SEntMan.HasComponent<StealthComponent>(user), Is.True);
        });
        await Server.WaitPost(() => Server.System<InventorySystem>().TryUnequip(user, slot, force: true));
        await Pair.RunSeconds(0.8f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEquipmentComponent>(item).Active, Is.False);
            Assert.That(SEntMan.HasComponent<StealthComponent>(user), Is.False);
            Assert.That(SEntMan.HasComponent<ActiveOrbitraRatvarSpectaclesComponent>(user), Is.False);
            if (slot != "eyes") return;
            Assert.That(SEntMan.GetComponent<OrbitraThermalVisionComponent>(item).Enabled, Is.False);
            Assert.That(SEntMan.GetComponent<EyeComponent>(user).VisibilityMask & (int) VisibilityFlags.Ghost, Is.Zero);
            Assert.That(SEntMan.GetComponent<BlindableComponent>(user).EyeDamage, Is.Zero);
        });
        await Server.WaitPost(() => Server.System<InventorySystem>().TryEquip(user, item, slot, force: true));
        await Pair.RunSeconds(0.3f);
        EntityUid replacement = default, mind = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<MindSystem>().TryGetMind(user, out mind, out _), Is.True);
            replacement = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Server.System<MindSystem>().TransferTo(mind, replacement);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEquipmentComponent>(item).Active, Is.False);
            Assert.That(SEntMan.HasComponent<StealthComponent>(user), Is.False);
            Assert.That(SEntMan.HasComponent<ActiveOrbitraRatvarSpectaclesComponent>(user), Is.False);
        });
        await Server.WaitPost(() =>
        {
            Server.System<InventorySystem>().TryUnequip(user, slot, force: true);
            Server.System<InventorySystem>().TryEquip(replacement, item, slot, force: true);
        });
        await Pair.RunSeconds(0.3f);
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<OrbitraRatvarRuleSystem>();
            Assert.That(system.TryGetCult(replacement, out var rule), Is.True);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEquipmentComponent>(item).Active, Is.True);
            Assert.That(system.TryPurify(rule, mind), Is.True);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEquipmentComponent>(item).Active, Is.False);
            Assert.That(SEntMan.HasComponent<StealthComponent>(replacement), Is.False);
            Assert.That(SEntMan.HasComponent<ActiveOrbitraRatvarSpectaclesComponent>(replacement), Is.False);
        });
    }

    private (EntityUid Rule, EntityUid User) PrepareCult(EntityCoordinates coordinates)
    {
        Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
        SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Energy = 2000;
        var user = SEntMan.SpawnEntity("MobHuman", coordinates);
        AddMember(user, rule, true);
        var sigil = SEntMan.SpawnEntity("OrbitraRatvarTransmissionSigil", coordinates);
        Server.System<OrbitraRatvarPowerSystem>().BindTransmission(
            (sigil, SEntMan.GetComponent<OrbitraRatvarTransmissionComponent>(sigil)), rule);
        return (rule, user);
    }

    private void AddMember(EntityUid body, EntityUid rule, bool player)
    {
        var minds = Server.System<MindSystem>();
        var mind = minds.CreateMind(player ? ServerSession!.UserId : null);
        minds.TransferTo(mind, body);
        var roles = Server.System<RoleSystem>();
        roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
        roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
        role!.Value.Comp2.Rule = rule;
        SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Members.Add(mind);
    }

    private EntityUid SpawnPoint(EntityUid rule, EntityCoordinates coordinates)
    {
        var point = SEntMan.SpawnEntity("OrbitraRatvarTravelPoint", coordinates);
        SEntMan.GetComponent<OrbitraRatvarStructureComponent>(point).Rule = rule;
        return point;
    }

    private void PrepareFloor(EntityUid grid, Tile tile)
    {
        var component = SEntMan.GetComponent<MapGridComponent>(grid);
        for (var x = 0; x <= 6; x++) Server.System<SharedMapSystem>().SetTile((grid, component), new Vector2i(x, 0), tile);
    }

    private TransformComponent Transform(EntityUid entity) => SEntMan.GetComponent<TransformComponent>(entity);
}
