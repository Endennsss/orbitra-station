using Content.Server._Orbitra.Lavaland;
using Content.Server.Shuttles.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Shuttles.Systems;

public sealed partial class DockingSystem
{
    [Dependency] private SharedPhysicsSystem _landingPhysics = default!;

    private List<Entity<DockingComponent>> GetAutomaticDockTargets(EntityUid grid)
    {
        var docks = GetDocks(grid);
        docks.RemoveAll(dock => HasComp<OrbitraLavalandReservedDockComponent>(dock.Owner));
        return docks;
    }

    /// <summary>Проверяет одну пару шлюзов, пока исходный шлюз шаттла ещё занят посадкой.</summary>
    public DockingConfig? GetDepartureDockingConfig(EntityUid shuttle, EntityUid grid,
        Entity<DockingComponent> mobile, Entity<DockingComponent> destination)
    {
        var configs = GetDockingConfigs(shuttle, grid, [mobile], [destination], allowDepartingDock: true);
        if (configs.Count == 0)
            return null;

        var config = configs[0];
        config.TargetGrid = grid;
        return GroundIsClear(shuttle, grid, config) ? config : null;
    }

    private bool GroundIsClear(EntityUid shuttle, EntityUid grid, DockingConfig config)
    {
        if (Transform(grid).MapUid is not {} map || !HasComp<MapGridComponent>(map))
            return true;

        // Запрашиваем только коллизии на самой поверхности; тайлы базальта и шлюзы аванпоста сюда не входят.
        var world = new Transform(_transform.ToMapCoordinates(config.Coordinates).Position,
            config.Angle + _transform.GetWorldRotation(grid));
        var local = _landingPhysics.GetRelativePhysicsTransform(world, map);
        var hits = new HashSet<EntityUid>();
        foreach (var fixture in Comp<FixturesComponent>(shuttle).Fixtures.Values)
        {
            if (fixture.Hard)
                _lookup.GetLocalEntitiesIntersecting(map, fixture.Shape, local, hits, LookupFlags.Static | LookupFlags.Dynamic);
        }
        foreach (var uid in hits)
        {
            var xform = Transform(uid);
            if (uid != grid && uid != shuttle && xform.Anchored && xform.ParentUid == map)
                return false;
        }
        return true;
    }
}
