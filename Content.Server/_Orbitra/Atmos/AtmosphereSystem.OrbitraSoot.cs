using Content.Server.Construction.Components;
using Content.Shared._Orbitra.Atmos;
using Content.Shared.Wall;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    private void OrbitraLeaveSoot(EntityUid grid, MapGridComponent mapGrid, Vector2i tile)
    {
        OrbitraMarkSootTile(grid, mapGrid, tile, includeMachines: true);
        OrbitraMarkSootTile(grid, mapGrid, tile + new Vector2i(1, 0));
        OrbitraMarkSootTile(grid, mapGrid, tile + new Vector2i(-1, 0));
        OrbitraMarkSootTile(grid, mapGrid, tile + new Vector2i(0, 1));
        OrbitraMarkSootTile(grid, mapGrid, tile + new Vector2i(0, -1));
    }

    private void OrbitraMarkSootTile(EntityUid grid, MapGridComponent mapGrid, Vector2i tile, bool includeMachines = false)
    {
        _entSet.Clear();
        _lookup.GetLocalEntitiesIntersecting(grid, tile, _entSet, 0f, gridComp: mapGrid);
        foreach (var entity in _entSet)
        {
            if (!TerminatingOrDeleted(entity) && Transform(entity).Anchored &&
                (HasComp<WallComponent>(entity) || includeMachines && HasComp<MachineComponent>(entity)))
                EnsureComp<OrbitraSootComponent>(entity);
        }
        _entSet.Clear();
    }
}
