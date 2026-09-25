using Content.Shared._Orbitra.Particles;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Particles;

/// <summary>Maintains a non-persistent grid index without requiring marker PVS overrides.</summary>
public sealed partial class OrbitraAmbientDustZoneSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, (EntityUid? Grid, Box2 Bounds)> _markers = new();
    private readonly HashSet<EntityUid> _dirtyGrids = new();
    private readonly List<EntityUid> _keys = new();
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraAmbientDustZoneComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<OrbitraAmbientDustZoneComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<OrbitraAmbientDustZoneComponent> ent, ref ComponentStartup args)
    {
        _markers[ent] = (null, default);
    }

    private void OnShutdown(Entity<OrbitraAmbientDustZoneComponent> ent, ref ComponentShutdown args)
    {
        if (_markers.Remove(ent, out var old) && old.Grid is { } grid)
            _dirtyGrids.Add(grid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        _keys.Clear();
        _keys.AddRange(_markers.Keys);
        foreach (var uid in _keys)
        {
            if (!TryComp<OrbitraAmbientDustZoneComponent>(uid, out var zone) || !TryComp(uid, out TransformComponent? xform))
                continue;
            var width = Math.Clamp(zone.Width, 1, 32);
            var height = Math.Clamp(zone.Height, 1, 32);
            if (width != zone.Width || height != zone.Height)
            {
                zone.Width = width;
                zone.Height = height;
                Dirty(uid, zone);
            }
            EntityUid? grid = null;
            var bounds = default(Box2);
            if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var gridComp))
            {
                grid = gridUid;
                var point = _transform.ToCoordinates(gridUid, _transform.GetMapCoordinates(uid, xform));
                bounds = OrbitraDust.Bounds(point.Position, width, height, gridComp.TileSize);
            }
            var old = _markers[uid];
            if (old.Grid == grid && old.Bounds == bounds)
                continue;
            if (old.Grid is { } oldGrid)
                _dirtyGrids.Add(oldGrid);
            if (grid is { } newGrid)
                _dirtyGrids.Add(newGrid);
            _markers[uid] = (grid, bounds);
        }
        foreach (var grid in _dirtyGrids)
        {
            if (TerminatingOrDeleted(grid))
                continue;
            var regions = new List<Box2>();
            foreach (var marker in _markers.Values)
            {
                if (marker.Grid == grid && !regions.Contains(marker.Bounds))
                    regions.Add(marker.Bounds);
            }
            var component = EnsureComp<OrbitraAmbientDustGridComponent>(grid);
            if (component.Regions.Count == regions.Count && component.Regions.TrueForAll(regions.Contains))
                continue;
            component.Regions = regions;
            Dirty(grid, component);
        }
        _dirtyGrids.Clear();
    }

    public override void Shutdown()
    {
        _markers.Clear();
        _dirtyGrids.Clear();
        _keys.Clear();
        base.Shutdown();
    }
}
