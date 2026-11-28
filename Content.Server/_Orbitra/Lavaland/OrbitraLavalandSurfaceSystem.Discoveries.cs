using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Orbitra.Lavaland;

public sealed partial class OrbitraLavalandSurfaceSystem
{
    [Dependency] private OrbitraAshFrontSystem _ash = default!;

    private static readonly EntProtoId TrackMech = "OrbitraAshMechTrack";
    private static readonly EntProtoId TrackCable = "OrbitraAshCable";
    private static readonly EntProtoId TrackStretcher = "OrbitraAshStretcherTrack";
    private static readonly ResPath RipleySite = new("/Maps/_Orbitra/Lavaland/ripley.yml");

    /// <summary>Находки создаются один раз вместе с планетой. Фронты открывают только следы к ним.</summary>
    private bool PlaceAshFinds(EntityUid map, MapId mapId, Random random)
    {
        var front = Comp<OrbitraAshFrontComponent>(map);
        var mechIndex = random.Next(Directions.Length);
        front.FirstSector = Directions[mechIndex].AsDirection();
        EntProtoId[] markers = [TrackMech, TrackCable, TrackStretcher];
        for (var i = 0; i < markers.Length; i++)
        {
            var direction = Directions[(mechIndex + i) % Directions.Length];
            var sector = direction.AsDirection();
            var start = front.Sites[sector].Floored() - direction * 22;
            var end = start + new Vector2i(-direction.Y, direction.X) * 16;
            var marker = markers[i];
            if (marker == TrackMech && !PlaceRipley(map, mapId, end))
                return false;

            PlaceAshTrail(map, sector, marker, start, end);
        }

        // Обе западные площадки выходят на тропу с востока. Ответвления ведут в обход стен.
        var greenhouse = front.Sites[Direction.NorthWest].Floored() + new Vector2i(16, 0);
        PlaceAshTrail(map, Direction.NorthWest, TrackCable, greenhouse, greenhouse + new Vector2i(0, 16));
        var mine = front.Sites[Direction.SouthWest].Floored() + new Vector2i(16, 0);
        PlaceAshTrail(map, Direction.SouthWest, TrackStretcher, mine, mine + new Vector2i(0, -16));
        return true;
    }

    private void PlaceAshTrail(EntityUid map, Direction sector, EntProtoId marker, Vector2i start, Vector2i end)
    {
        Trail(map, start, end);
        _ash.RegisterTrail(map, sector, marker, start, end);
        if (marker == TrackMech)
            return;

        Reserve(map, new Box2(end - 2, end + 3));
        if (marker == TrackCable)
        {
            SpawnAttachedTo("SubstationBasicEmpty", TileCenter(map, end));
            SpawnAttachedTo("ToolboxElectricalFilled", TileCenter(map, end + new Vector2i(1, 0)));
            SpawnAttachedTo("PowerCellMedium", TileCenter(map, end + new Vector2i(1, 1)));
        }
        else
        {
            SpawnAttachedTo("RollerBed", TileCenter(map, end));
            SpawnAttachedTo("MedkitBruteFilled", TileCenter(map, end + new Vector2i(1, 0)));
            SpawnAttachedTo("FlashlightLantern", TileCenter(map, end + new Vector2i(-1, 0)));
        }
    }

    private bool PlaceRipley(EntityUid map, MapId mapId, Vector2i entrance)
    {
        var origin = entrance - new Vector2i(1, -2);
        if (!_loader.TryLoadGrid(mapId, RipleySite, out var grid, offset: origin))
            return false;

        PlaceGroundGrid(grid.Value, origin);
        _metadata.SetEntityName(grid.Value, Loc.GetString("orbitra-lavaland-ripley-site"));
        Reserve(map, grid.Value.Comp.LocalAABB.Translated(origin).Enlarged(3));
        return true;
    }
}
