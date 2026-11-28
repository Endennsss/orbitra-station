using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server._Orbitra.Lavaland;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Cargo.Systems;
using Content.Server.Parallax;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CombatMode;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra.Lavaland;

[TestFixture, NonParallelizable]
public sealed class OrbitraLavalandSurfaceTest
{
    [TestCase(7)]
    [TestCase(41)]
    public async Task SurfaceHasWalkableRoutesToThreeRuinsBothWorksitesAndOneRipley(int seed)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var ent = server.EntMan;
        var previous = pair.Player!.AttachedEntity;
        EntityUid map = default;
        EntityUid outpost = default;
        EntityUid observer = default;
        try
        {
            await server.WaitAssertion(() =>
            {
                Assert.That(server.System<OrbitraLavalandSurfaceSystem>().TryCreate(seed, out map, out outpost), Is.True);
                observer = ent.SpawnAttachedTo("MobHuman", new EntityCoordinates(map, 24.5f, 10.5f));
                server.PlayerMan.SetAttachedEntity(pair.Player!, observer);
            });
            await pair.RunTicksSync(3);
            await server.WaitAssertion(() =>
            {
                var maps = server.System<SharedMapSystem>();
                var biome = ent.GetComponent<BiomeComponent>(map);
                Assert.That(biome.Seed, Is.EqualTo(seed));
                Assert.That(biome.LoadedChunks.Count, Is.Positive);
                Assert.That(ent.HasComponent<LightCycleComponent>(map), Is.False);
                var mixture = server.System<AtmosphereSystem>().GetTileMixture(observer);
                Assert.That(mixture, Is.Not.Null);
                Assert.That(mixture!.Pressure, Is.InRange(95f, 108f));
                Assert.That(mixture.GetMoles(Gas.Oxygen) / mixture.TotalMoles, Is.InRange(0.20f, 0.22f));

                var terrain = ent.GetComponent<MapGridComponent>(map);
                for (var x = 14; x <= 34; x++)
                for (var y = 9; y <= 11; y++)
                {
                    var tile = new Vector2i(x, y);
                    Assert.That(maps.GetTileRef(map, terrain, tile).Tile.IsEmpty, Is.False, tile.ToString());
                    foreach (var uid in maps.GetAnchoredEntities(map, terrain, tile))
                        Assert.Fail($"Unexpected obstacle {ent.GetComponent<MetaDataComponent>(uid).EntityPrototype} at {tile}");
                }

                var mapId = ent.GetComponent<TransformComponent>(map).MapID;
                var ruins = maps.GetAllGrids(mapId).ToList().Where(g => g.Owner != map && g.Owner != outpost).ToArray();
                Assert.That(ruins, Has.Length.EqualTo(6));
                var front = ent.GetComponent<OrbitraAshFrontComponent>(map);
                var forecastSites = front.Sites;
                Assert.That(forecastSites.Keys, Is.EquivalentTo(new[]
                {
                    Direction.East, Direction.North, Direction.South, Direction.NorthWest, Direction.SouthWest,
                }));
                var loc = server.ResolveDependency<ILocalizationManager>();
                var loot = new Dictionary<string, string>
                {
                    [loc.GetString("orbitra-lavaland-ruin-crasheddropship")] = "ScrapSteel",
                    [loc.GetString("orbitra-lavaland-ruin-generator-scrapyard")] = "PowerCellHigh",
                    [loc.GetString("orbitra-lavaland-ruin-miner-tomb")] = "Pickaxe",
                    [loc.GetString("orbitra-lavaland-ruin-wrecked-outpost")] = "Brutepack",
                    [loc.GetString("orbitra-lavaland-ruin-preserved-terrarium")] = "TomatoSeeds",
                    [loc.GetString("orbitra-lavaland-ruin-mineshaft")] = "OreProcessorIndustrial",
                    [loc.GetString("orbitra-lavaland-ripley-site")] = "OrbitraMechRipleyAbandoned",
                };
                var paths = ReachableWithCargo(map, terrain, maps);
                var definitions = server.ResolveDependency<ITileDefinitionManager>();
                foreach (var grid in maps.GetAllGrids(mapId))
                {
                    if (grid.Owner == map)
                        continue;
                    foreach (var cell in maps.GetAllTiles(grid.Owner, grid.Comp))
                    {
                        var definition = definitions[cell.Tile.TypeId];
                        if (cell.Tile.Variant >= definition.Variants)
                            Assert.Fail($"{definition.ID}, variant {cell.Tile.Variant} at {cell.GridIndices}");
                    }
                }
                var names = new HashSet<string>();
                Vector2i? mechEntrance = null;
                foreach (var ruin in ruins)
                {
                    var name = ent.GetComponent<MetaDataComponent>(ruin).EntityName;
                    Assert.That(names.Add(name), Is.True);
                    var children = ent.GetComponent<TransformComponent>(ruin).ChildEnumerator;
                    var items = new HashSet<string>();
                    while (children.MoveNext(out var child))
                    {
                        if (ent.GetComponent<MetaDataComponent>(child).EntityPrototype is { } prototype)
                            items.Add(prototype.ID);
                    }
                    Assert.That(items, Does.Contain(loot[name]), name);
                    var origin = server.System<SharedTransformSystem>().GetWorldPosition(ruin.Owner);
                    var walls = ruin.Comp.LocalAABB.Translated(origin);
                    if (name == loc.GetString("orbitra-lavaland-ripley-site"))
                    {
                        Assert.That(items, Does.Contain("LightPostSmall"), "The mech must be visible inside the alcove.");
                        // Из кабины на юг ведёт проём шириной в три клетки, за ним начинается обратная тропа.
                        for (var x = 0; x < 3; x++)
                        for (var y = -1; y <= 1; y++)
                        {
                            foreach (var obj in maps.GetAnchoredEntities(ruin.Owner, ruin.Comp, new Vector2i(x, y)))
                                Assert.That(ent.GetComponent<MetaDataComponent>(obj).EntityPrototype?.ID,
                                    Is.Not.EqualTo("WallRockBasalt"));
                        }
                        mechEntrance = origin.Floored() + new Vector2i(1, -2);
                        Assert.That(paths, Does.Contain(mechEntrance.Value));
                    }
                    else
                        Assert.That(forecastSites.Values.Count(point => walls.Contains(point)), Is.EqualTo(1), name);
                    Assert.That(paths.Any(tile => walls.Enlarged(3).Contains(tile) && !walls.Contains(tile)), Is.True,
                        $"No route for a stretcher to {name}");
                }
                Assert.That(names, Does.Contain(loc.GetString("orbitra-lavaland-ruin-preserved-terrarium")));
                Assert.That(names, Does.Contain(loc.GetString("orbitra-lavaland-ruin-mineshaft")));
                Assert.That(names, Does.Contain(loc.GetString("orbitra-lavaland-ripley-site")));

                var remaining = new HashSet<string> { "WallRockTin", "WallRockQuartz", "WallRockGold", "WallRockSilver", "WallRockPlasma", "MobGoliath", "MobWatcherLavaland" };
                var finds = new Dictionary<Vector2i, string>();
                var inhabitants = ent.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
                while (inhabitants.MoveNext(out var uid, out var meta, out var transform))
                {
                    if (transform.ParentUid != map || meta.EntityPrototype is not { } proto)
                        continue;
                    if (proto.ID is "MobGoliath" or "MobWatcherLavaland")
                        Assert.That(Vector2.Distance(transform.LocalPosition, new Vector2(13.5f, 10.5f)), Is.GreaterThan(16f), uid.ToString());
                    if (proto.ID is "SubstationBasicEmpty" or "RollerBed")
                        finds[transform.LocalPosition.Floored()] = proto.ID;
                    remaining.Remove(proto.ID);
                }
                Assert.That(remaining, Is.Empty);
                Assert.That(front.Trails.Keys, Is.EquivalentTo(forecastSites.Keys));
                foreach (var (sector, trail) in front.Trails)
                {
                    var clue = trail.Values.First();
                    var step = clue.Heading.ToIntVec();
                    var start = trail.Keys.Single(tile => !trail.ContainsKey(tile - step));
                    var end = trail.Keys.Single(tile => !trail.ContainsKey(tile + step));
                    Assert.That(paths, Does.Contain(start));
                    if (sector == front.FirstSector)
                    {
                        Assert.That(clue.Marker.Id, Is.EqualTo("OrbitraAshMechTrack"));
                        Assert.That(end, Is.EqualTo(mechEntrance));
                    }
                    else
                    {
                        var target = clue.Marker.Id == "OrbitraAshCable" ? "SubstationBasicEmpty" : "RollerBed";
                        Assert.That(finds[end], Is.EqualTo(target));
                    }
                }
                Assert.That(ent.GetComponent<FlammableComponent>(observer).OnFire, Is.False);

                // Ищем лаву за пределами зарезервированных троп и заходим в настоящий загружаемый чанк.
                var terrainSystem = server.System<BiomeSystem>();
                var lava = Vector2i.Zero;
                for (var x = 70; x < 110 && lava == Vector2i.Zero; x++)
                for (var y = 35; y < 75; y++)
                {
                    var tile = new Vector2i(x, y);
                    if (!terrainSystem.TryGetEntity(tile, biome, (map, terrain), out var proto) || proto != "FloorLavaEntity")
                        continue;
                    lava = tile;
                    break;
                }
                Assert.That(lava, Is.Not.EqualTo(Vector2i.Zero));
                server.System<SharedTransformSystem>().SetCoordinates(observer,
                    new EntityCoordinates(map, (Vector2) lava + new Vector2(0.5f)));
            });
            await pair.RunTicksSync(12);
            await server.WaitAssertion(() => Assert.That(ent.GetComponent<FlammableComponent>(observer).OnFire, Is.True));
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.PlayerMan.SetAttachedEntity(pair.Player!, previous);
                if (ent.EntityExists(map))
                    ent.DeleteEntity(map);
            });
        }
        await pair.CleanReturnAsync();
    }

    private static HashSet<Vector2i> ReachableWithCargo(EntityUid map, MapGridComponent terrain, SharedMapSystem maps)
    {
        // Проверяем проход для носилок с сопровождающими на уже построенном грунте.
        var start = new Vector2i(15, 10);
        var reachable = new HashSet<Vector2i> { start };
        var seen = new HashSet<Vector2i> { start };
        var queue = new Queue<Vector2i>();
        queue.Enqueue(start);
        Vector2i[] neighbors = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

        while (queue.TryDequeue(out var tile))
        {
            foreach (var step in neighbors)
            {
                var next = tile + step;
                if (!seen.Add(next) || !IsWideEnough(next))
                    continue;
                reachable.Add(next);
                queue.Enqueue(next);
            }
        }
        return reachable;

        bool IsWideEnough(Vector2i center)
        {
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
            {
                var tile = center + new Vector2i(x, y);
                if (!maps.TryGetTileRef(map, terrain, tile, out var ground) || ground.Tile.IsEmpty ||
                    maps.GetAnchoredEntities(map, terrain, tile).MoveNext(out _))
                    return false;
            }
            return true;
        }
    }

    [Test]
    public async Task MinedVeinAndCrateSurviveChunkUnload()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var ent = server.EntMan;
        var previous = pair.Player!.AttachedEntity;
        EntityUid map = default;
        EntityUid worker = default;
        EntityUid crate = default;
        EntityUid oreWall = default;
        EntityUid pickaxe = default;
        EntityUid oreDrop = default;
        try
        {
            await server.WaitAssertion(() =>
            {
                Assert.That(server.System<OrbitraLavalandSurfaceSystem>().TryCreate(23, out map, out _), Is.True);
                var site = new EntityCoordinates(map, 32.5f, 12.5f);
                worker = ent.SpawnAttachedTo("MobHuman", site);
                crate = ent.SpawnAttachedTo("CrateGenericSteel", new EntityCoordinates(map, 33.5f, 10.5f));
                pickaxe = ent.SpawnAttachedTo("Pickaxe", site);
                Assert.That(server.System<SharedHandsSystem>().TryPickup(worker, pickaxe), Is.True);
                server.System<SharedCombatModeSystem>().SetInCombatMode(worker, true);
                server.PlayerMan.SetAttachedEntity(pair.Player!, worker);
            });
            await pair.RunSeconds(2);
            await server.WaitAssertion(() =>
            {
                var maps = server.System<SharedMapSystem>();
                var terrain = ent.GetComponent<MapGridComponent>(map);
                var wallTile = new Vector2i(32, 13);
                foreach (var uid in maps.GetAnchoredEntities(map, terrain, wallTile))
                {
                    if (ent.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "WallRockTin")
                        oreWall = uid;
                }
                Assert.That(ent.EntityExists(oreWall), Is.True);
                var weapon = ent.GetComponent<MeleeWeaponComponent>(pickaxe);
                Assert.That(server.System<SharedMeleeWeaponSystem>().AttemptLightAttack(worker, pickaxe, weapon, oreWall), Is.True);
            });
            await pair.RunTicksSync(2);
            await server.WaitAssertion(() =>
            {
                Assert.That(ent.EntityExists(oreWall), Is.False);
                var drops = ent.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
                while (drops.MoveNext(out var uid, out var meta, out var transform))
                {
                    if (transform.MapUid == map && meta.EntityPrototype?.ID == "SteelOre1" &&
                        Vector2.Distance(transform.LocalPosition, new Vector2(32.5f, 13.5f)) < 1f)
                    {
                        oreDrop = uid;
                        break;
                    }
                }
                Assert.That(ent.EntityExists(oreDrop), Is.True);
                server.PlayerMan.SetAttachedEntity(pair.Player!, previous);
            });
            await pair.RunTicksSync(2);
            await server.WaitAssertion(() =>
            {
                Assert.That(ent.GetComponent<BiomeComponent>(map).LoadedChunks, Is.Empty);
                server.PlayerMan.SetAttachedEntity(pair.Player!, worker);
            });
            await pair.RunTicksSync(2);
            await server.WaitAssertion(() =>
            {
                Assert.That(ent.GetComponent<BiomeComponent>(map).LoadedChunks, Is.Not.Empty);
                var terrain = ent.GetComponent<MapGridComponent>(map);
                foreach (var uid in server.System<SharedMapSystem>().GetAnchoredEntities(map, terrain, new Vector2i(32, 13)))
                    Assert.That(ent.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID, Is.Not.EqualTo("WallRockTin"));
                Assert.That(ent.GetComponent<TransformComponent>(crate).MapUid, Is.EqualTo(map));
                Assert.That(ent.GetComponent<TransformComponent>(oreDrop).MapUid, Is.EqualTo(map));
                Assert.That(server.System<PricingSystem>().GetPrice(oreDrop), Is.Positive);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.PlayerMan.SetAttachedEntity(pair.Player!, previous);
                if (ent.EntityExists(map))
                    ent.DeleteEntity(map);
            });
        }
        await pair.CleanReturnAsync();
    }
}
