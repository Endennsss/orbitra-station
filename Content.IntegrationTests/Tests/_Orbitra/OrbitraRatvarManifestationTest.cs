using System;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.Atmos.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Atmos.Components;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Checks finale authority, one-shot effects and safe territory updates.</summary>
[TestFixture]
public sealed class OrbitraRatvarManifestationTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [Test]
    public async Task PulseIsStationLocalAndOneShot()
    {
        var map = await Pair.CreateTestMap();
        var other = await Pair.CreateTestMap();
        EntityUid rule = default, god = default, victim = default, servant = default, remote = default;
        OrbitraRatvarRuleComponent cult = default!;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(CultRule, out rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationDataComponent>(station);
            Server.System<StationSystem>().AddGridToStation(station, map.Grid);
            cult.Station = station;
            cult.Won = true;
            cult.FinishAt = Server.Resolve<IGameTiming>().CurTime + TimeSpan.FromMinutes(1);
            god = SEntMan.SpawnEntity("OrbitraRatvarFinale", map.GridCoords);
            cult.Manifestation = god;
            var manifestation = SEntMan.GetComponent<OrbitraRatvarManifestationComponent>(god);
            manifestation.Rule = rule;
            manifestation.Grid = map.Grid;
            manifestation.Map = map.MapUid;
            victim = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            servant = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            remote = SEntMan.SpawnEntity("MobHuman", other.GridCoords);
            var mind = Server.System<MindSystem>().CreateMind(null);
            Server.System<MindSystem>().TransferTo(mind, servant);
            Server.System<RoleSystem>().MindAddRole(mind, "OrbitraMindRoleRatvar");
            Server.System<RoleSystem>().MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
            role!.Value.Comp2.Rule = rule;
            cult.Members.Add(mind);
            cult.FinalePulseAt = Server.Resolve<IGameTiming>().CurTime;
            Server.System<OrbitraRatvarRuleSystem>().Update(0);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(cult.FinalePulseAt, Is.Null);
            Assert.That(SEntMan.GetComponent<FlammableComponent>(victim).OnFire, Is.True);
            Assert.That(SEntMan.GetComponent<FlammableComponent>(servant).OnFire, Is.False);
            Assert.That(SEntMan.GetComponent<FlammableComponent>(remote).OnFire, Is.False);
        });
        await Server.WaitPost(() =>
        {
            Server.System<FlammableSystem>().SetFireStacks(victim, 0);
            Server.System<OrbitraRatvarRuleSystem>().Update(0);
        });
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<FlammableComponent>(victim).FireStacks, Is.Zero));
        await Server.WaitPost(() => Server.System<GameTicker>().EndGameRule(rule));
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(cult.FinishAt, Is.Null);
            Assert.That(cult.Manifestation, Is.Null);
            Assert.That(SEntMan.EntityExists(god), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task TerritoryRequiresBoundVictoryAndPreservesInfrastructure(bool bound)
    {
        var map = await Pair.CreateTestMap();
        EntityUid pipe = default, cable = default, item = default;
        var transformed = false;
        await Server.WaitPost(() =>
        {
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationDataComponent>(station);
            Server.System<StationSystem>().AddGridToStation(station, map.Grid);
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Station = station;
            cult.Won = true;
            cult.FinishAt = Server.Resolve<IGameTiming>().CurTime + TimeSpan.FromMinutes(1);
            var god = SEntMan.SpawnEntity("OrbitraRatvarFinale", map.GridCoords);
            var manifestation = SEntMan.GetComponent<OrbitraRatvarManifestationComponent>(god);
            manifestation.Radius = 0;
            manifestation.TileBudget = 1;
            if (bound)
            {
                manifestation.Rule = rule;
                manifestation.Grid = map.Grid;
                manifestation.Map = map.MapUid;
                cult.Manifestation = god;
            }
            var mapping = Server.System<SharedMapSystem>();
            mapping.SetTile(map.Grid, map.Tile.GridIndices,
                new Tile(Server.Resolve<ITileDefinitionManager>()["FloorSteel"].TileId));
            pipe = SEntMan.SpawnEntity("GasPipeStraight", map.GridCoords);
            cable = SEntMan.SpawnEntity("CableHV", map.GridCoords);
            item = SEntMan.SpawnEntity("Crowbar", map.GridCoords);
            transformed = Server.System<OrbitraRatvarManifestationSystem>().TryTransformTerritory((god, manifestation));
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(transformed, Is.EqualTo(bound));
            var tile = Server.System<SharedMapSystem>().GetTileRef(map.Grid, map.Grid.Comp, map.GridCoords);
            Assert.That(Server.Resolve<ITileDefinitionManager>()[tile.Tile.TypeId].ID,
                Is.EqualTo(bound ? "OrbitraRatvarFloor" : "FloorSteel"));
            Assert.That(SEntMan.EntityExists(item), Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(pipe).Anchored, Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(cable).Anchored, Is.True);
        });
    }

    [TestCase("WallSolid", "WallBrass")]
    [TestCase("ReinforcedWindow", "OrbitraRatvarWindow")]
    [TestCase("Airlock", "OrbitraRatvarDoor")]
    public async Task TerritoryConvertsOnlySupportedStructureAndKeepsContents(string source, string result)
    {
        var map = await Pair.CreateTestMap();
        EntityUid original = default, pipe = default, cable = default;
        EntityUid? board = null;
        await Server.WaitPost(() =>
        {
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationDataComponent>(station);
            Server.System<StationSystem>().AddGridToStation(station, map.Grid);
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Station = station;
            cult.Won = true;
            cult.FinishAt = Server.Resolve<IGameTiming>().CurTime + TimeSpan.FromMinutes(1);
            var god = SEntMan.SpawnEntity("OrbitraRatvarFinale", map.GridCoords);
            var manifestation = SEntMan.GetComponent<OrbitraRatvarManifestationComponent>(god);
            manifestation.Radius = 0;
            manifestation.TileBudget = 1;
            manifestation.Rule = rule;
            manifestation.Grid = map.Grid;
            manifestation.Map = map.MapUid;
            manifestation.NextMove = TimeSpan.MaxValue;
            cult.Manifestation = god;
            original = SEntMan.SpawnEntity(source, map.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            if (containers.TryGetContainer(original, "board", out var electronics))
                board = electronics.ContainedEntities.Single();
            pipe = SEntMan.SpawnEntity("GasPipeStraight", map.GridCoords);
            cable = SEntMan.SpawnEntity("CableHV", map.GridCoords);
            Server.System<OrbitraRatvarManifestationSystem>().TryTransformTerritory((god, manifestation));
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(original), Is.False);
            var converted = Server.System<SharedMapSystem>().GetAnchoredEntities(map.Grid, map.Grid.Comp, map.Tile.GridIndices)
                .Where(uid => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == result).ToArray();
            Assert.That(converted, Has.Length.EqualTo(1));
            Assert.That(SEntMan.GetComponent<TransformComponent>(pipe).Anchored, Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(cable).Anchored, Is.True);
            if (board is { } originalBoard)
            {
                Assert.That(SEntMan.EntityExists(originalBoard), Is.True);
                Assert.That(Server.System<SharedContainerSystem>().TryGetContainer(converted[0], "board", out var electronics), Is.True);
                Assert.That(electronics!.ContainedEntities, Does.Contain(originalBoard));
            }
        });
    }
}
