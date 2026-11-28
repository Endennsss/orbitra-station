using System.Linq;
using System.Numerics;
using Content.Server.Parallax;
using Content.Server.Shuttles.Systems;
using Content.Shared.Light.Components;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Orbitra.Lavaland;

/// <summary>Создаёт поверхность, аванпост и пешие маршруты к руинам при первом рейсе смены.</summary>
public sealed partial class OrbitraLavalandSurfaceSystem : EntitySystem
{
    [Dependency] private BiomeSystem _biomes = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<BiomeTemplatePrototype> LavaBiome = "Lava";
    private static readonly Vector2i Exit = new(13, 10);
    private static readonly Vector2i Trailhead = new(35, 10);
    private static readonly string[] Ruins = ["crasheddropship", "generator_scrapyard", "miner_tomb", "wrecked_outpost"];
    private static readonly Vector2i[] Directions = [new(1, 0), new(0, 1), new(0, -1)];
    private static readonly string[] FarOres = ["WallRockPlasma", "WallRockGold", "WallRockSilver"];

    public bool TryCreate(int seed, out EntityUid map, out EntityUid outpost)
    {
        map = default;
        outpost = default;
        var surface = _maps.CreateMap(out var mapId);
        try
        {
            _biomes.EnsurePlanet(surface, ProtoMan.Index(LavaBiome), seed,
                mapLight: Color.FromHex("#E3C4A8"));
            RemComp<LightCycleComponent>(surface);
            _metadata.SetEntityName(surface, Loc.GetString("orbitra-lavaland-surface-name"));

            if (!_loader.TryLoadGrid(mapId, new ResPath("/Maps/_Orbitra/Lavaland/mining_outpost.yml"), out var station))
                return false;
            PlaceGroundGrid(station.Value, Vector2.Zero);
            _metadata.SetEntityName(station.Value, Loc.GetString("orbitra-lavaland-outpost-name"));

            // Резервируем базальт до первого визита, пока биом ещё не разместил лаву и камни.
            Reserve(surface, station.Value.Comp.LocalAABB.Enlarged(2));
            Reserve(surface, new Box2(-9, 14, 28, 38));
            Reserve(surface, new Box2(Exit - 16, Exit + 17));
            Trail(surface, Exit, Trailhead);
            Vein(surface, new Vector2i(30, 13), "WallRockTin");
            Vein(surface, new Vector2i(30, 7), "WallRockQuartz");

            var random = new System.Random(seed);
            var ruins = (string[]) Ruins.Clone();
            for (var i = 0; i < Directions.Length; i++)
            {
                var choice = random.Next(i, ruins.Length);
                (ruins[i], ruins[choice]) = (ruins[choice], ruins[i]);
                if (!PlaceExpeditionSite(surface, mapId, Directions[i], ruins[i], i, random))
                    return false;
            }

            if (!PlaceWorksite(surface, mapId, "preserved_terrarium", Direction.NorthWest, new(-42, 62), new(11, -2)) ||
                !PlaceWorksite(surface, mapId, "mineshaft", Direction.SouthWest, new(-50, -56), new(30, -13)))
                return false;

            if (!PlaceAshFinds(surface, mapId, random))
                return false;

            var biome = Comp<BiomeComponent>(surface);
            foreach (var marker in new[] { "OreIron", "OreQuartz", "OreGold", "OreSilver", "OrePlasma" })
                _biomes.AddMarkerLayer(surface, biome, marker);

            map = surface;
            outpost = station.Value.Owner;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Could not create Lavaland (seed {seed}): {ex}");
            return false;
        }
        finally
        {
            if (!map.Valid)
                _maps.DeleteMap(mapId);
        }
    }

    private bool PlaceExpeditionSite(EntityUid map, MapId mapId, Vector2i direction, string ruin, int index, System.Random random)
    {
        var side = new Vector2i(-direction.Y, direction.X);
        var corner = Trailhead + direction * random.Next(25, 35);
        var bend = corner + side * (random.Next(6, 12) * (random.Next(2) == 0 ? 1 : -1));
        var center = bend + direction * random.Next(50, 66);

        var path = new ResPath($"/Maps/Lavaland/{ruin}.yml");
        // Заброшенный аванпост сохранён как карта с одним гридом, остальные руины как отдельные гриды.
        Entity<MapGridComponent>? grid;
        if (ruin == "wrecked_outpost")
        {
            if (!_loader.TryMergeMap(mapId, path, out var grids, offset: center) || grids.Count != 1)
                return false;
            grid = grids.Single();
        }
        else if (!_loader.TryLoadGrid(mapId, path, out grid, offset: center))
            return false;

        var bounds = grid.Value.Comp.LocalAABB;
        var origin = center - bounds.Center.Floored();
        PlaceGroundGrid(grid.Value, origin);
        _metadata.SetEntityName(grid.Value, Loc.GetString($"orbitra-lavaland-ruin-{ruin.Replace('_', '-')}"));
        Reserve(map, bounds.Translated(origin).Enlarged(3));
        Trail(map, Trailhead, corner);
        Trail(map, corner, bend);
        Trail(map, bend, center);
        EnsureComp<OrbitraAshFrontComponent>(map).Sites[direction.AsDirection()] = center;

        var mine = bend + direction * random.Next(12, 20);
        Trail(map, mine, mine + side * 5);
        Vein(map, mine + side * 5, FarOres[index]);
        var nest = mine + direction * random.Next(12, 17) - side * 5;
        Reserve(map, new Box2(nest - 2, nest + 3));
        SpawnAttachedTo(index == 0 ? "MobGoliath" : "MobWatcherLavaland", TileCenter(map, nest));
        return true;
    }

    private bool PlaceWorksite(EntityUid map, MapId mapId, string name, Direction sector, Vector2i entrance, Vector2i localEntrance)
    {
        var origin = entrance - localEntrance;
        var path = new ResPath($"/Maps/_Orbitra/Lavaland/{name}.yml");
        if (!_loader.TryLoadGrid(mapId, path, out var grid, offset: origin))
            return false;

        PlaceGroundGrid(grid.Value, origin);
        _metadata.SetEntityName(grid.Value, Loc.GetString($"orbitra-lavaland-ruin-{name.Replace('_', '-')}"));
        Reserve(map, grid.Value.Comp.LocalAABB.Translated(origin).Enlarged(3));

        // Ответвления начинаются за восточным выходом и подходят с грузовой стороны обоих зданий.
        var junction = new Vector2i(24, Exit.Y);
        var turn = new Vector2i(junction.X, entrance.Y);
        Trail(map, junction, turn);
        Trail(map, turn, entrance);
        EnsureComp<OrbitraAshFrontComponent>(map).Sites[sector] = entrance;
        return true;
    }

    private void PlaceGroundGrid(Entity<MapGridComponent> grid, Vector2 position)
    {
        // Старые карты указывают варианты, убранные из атласа полов. Сохраняем тип и ориентацию плитки.
        var replacements = new List<(Vector2i, Tile)>();
        foreach (var cell in _maps.GetAllTiles(grid.Owner, grid.Comp))
        {
            var tile = cell.Tile;
            if (tile.Variant >= _tiles[tile.TypeId].Variants)
                replacements.Add((cell.GridIndices, new Tile(tile.TypeId, tile.Flags, rotationMirroring: tile.RotationMirroring)));
        }
        _maps.SetTiles(grid, replacements);

        _transform.SetWorldPosition(grid, position);
        _shuttles.Disable(grid);
    }

    private void Vein(EntityUid map, Vector2i start, string wall)
    {
        Reserve(map, new Box2(start - 2, start + new Vector2i(5, 2)));
        for (var i = 0; i < 3; i++)
            SpawnAttachedTo(wall, TileCenter(map, start + new Vector2i(i, 0)));
        SpawnAttachedTo(wall, TileCenter(map, start + new Vector2i(1, 1)));
    }

    private void Trail(EntityUid map, Vector2i start, Vector2i end)
    {
        var bounds = Box2.FromTwoPoints(start, end);
        Reserve(map, new Box2(bounds.BottomLeft - Vector2.One, bounds.TopRight + new Vector2(2)));
    }

    private void Reserve(EntityUid map, Box2 area)
    {
        _biomes.ReserveTiles(map, area, new List<(Vector2i, Tile)>());
    }

    private static EntityCoordinates TileCenter(EntityUid map, Vector2i tile)
    {
        return new EntityCoordinates(map, (Vector2) tile + new Vector2(0.5f));
    }
}
