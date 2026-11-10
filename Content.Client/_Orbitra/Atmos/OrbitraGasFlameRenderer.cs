using System.Numerics;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Atmos;

internal sealed partial class OrbitraGasFlameRenderer : IDisposable
{
    private static readonly ProtoId<ShaderPrototype> FlameShader = "OrbitraGasFlame";

    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private ILogManager _logs = default!;

    private readonly SharedMapSystem _maps;
    private readonly SharedTransformSystem _transform;
    private readonly EntityQuery<GasTileOverlayComponent> _overlays;
    private readonly OrbitraGasCache<ChunkSurface> _cache = new();
    private readonly Vector4[] _fieldScratch = new Vector4[OrbitraGasField.FieldSize * OrbitraGasField.FieldSize];
    private readonly List<VisibleChunk> _visible = new(OrbitraGasCache<ChunkSurface>.Capacity);
    private readonly HashSet<(EntityUid Grid, Vector2i Index)> _visibleKeys = new();
    private readonly Func<(EntityUid Grid, Vector2i Index), bool> _isVisible;
    private readonly Func<(EntityUid Grid, Vector2i Index), bool> _isAvailable;
    private List<Entity<MapGridComponent>> _grids = new();
    private ShaderPrototype? _prototype;
    private float _time;
    private bool _unavailable;

    private readonly record struct VisibleChunk(EntityUid Grid, GasTileOverlayComponent Overlay, Vector2i Index,
        Matrix3x2 Transform, Box2 Bounds);

    public OrbitraGasFlameRenderer()
    {
        IoCManager.InjectDependencies(this);
        _maps = _entities.System<SharedMapSystem>();
        _transform = _entities.System<SharedTransformSystem>();
        _overlays = _entities.GetEntityQuery<GasTileOverlayComponent>();
        _isVisible = _visibleKeys.Contains;
        _isAvailable = IsAvailable;
    }

    public void Advance(float seconds)
    {
        _time += seconds;
        _cache.Retain(_isAvailable);
    }

    public bool TryDraw(in OverlayDrawArgs args)
    {
        if (_unavailable)
            return false;

        _cache.ChangeMap(args.MapId);
        try
        {
            if (!PrepareChunks(args))
                return false;
        }
        catch (Exception error)
        {
            _logs.GetSawmill("orbitra.flame").Warning($"Не удалось подготовить шейдер огня, включён штатный оверлей: {error}");
            _unavailable = true;
            Dispose();
            return false;
        }

        if (_visible.Count == 0)
            return true;

        var handle = args.WorldHandle;
        var originalTransform = handle.GetTransform();
        var originalShader = handle.GetShader();
        var reducedMotion = _configuration.GetCVar(CCVars.ReducedMotion);
        try
        {
            foreach (var chunk in _visible)
            {
                var surface = _cache.Entries[(chunk.Grid, chunk.Index)];
                handle.SetTransform(chunk.Transform);
                surface.Draw(handle, chunk.Bounds, _time, reducedMotion);
            }
        }
        finally
        {
            handle.UseShader(originalShader);
            handle.SetTransform(originalTransform);
        }

        return true;
    }

    private bool PrepareChunks(in OverlayDrawArgs args)
    {
        _visible.Clear();
        _visibleKeys.Clear();
        _grids.Clear();
        _maps.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _grids);
        foreach (var grid in _grids)
        {
            if (!_overlays.TryGetComponent(grid.Owner, out var overlay))
                continue;

            var (_, _, matrix, inverse) = _transform.GetWorldPositionRotationMatrixWithInv(grid.Owner);
            var localBounds = inverse.TransformBox(args.WorldBounds).Enlarged(grid.Comp.TileSize);
            var chunkSize = new Vector2(OrbitraGasField.Tiles * grid.Comp.TileSize);
            foreach (var (index, chunk) in overlay.Chunks)
            {
                var bounds = Box2.FromDimensions((Vector2) chunk.Origin * grid.Comp.TileSize, chunkSize);
                if (!bounds.Intersects(localBounds))
                    continue;

                var key = (grid.Owner, index);
                if ((!_cache.Entries.TryGetValue(key, out var surface) || !surface.MayBeVisible(_time)) &&
                    !HasFire(chunk))
                    continue;

                if (_visible.Count == OrbitraGasCache<ChunkSurface>.Capacity)
                {
                    _cache.Dispose();
                    return false;
                }

                _visibleKeys.Add(key);
                _visible.Add(new VisibleChunk(grid.Owner, overlay, index, matrix, bounds));
            }
        }

        _cache.Retain(_isVisible);
        foreach (var chunk in _visible)
        {
            var key = (chunk.Grid, chunk.Index);
            if (!_cache.Entries.TryGetValue(key, out var surface))
            {
                _prototype ??= _prototypes.Index(FlameShader);
                surface = new ChunkSurface(_clyde, _prototype);
                _cache.Entries.Add(key, surface);
            }

            surface.Update(chunk.Overlay.Chunks, chunk.Index, _fieldScratch, _time);
        }

        return true;
    }

    private bool IsAvailable((EntityUid Grid, Vector2i Index) key) =>
        _overlays.TryGetComponent(key.Grid, out var overlay) && overlay.Chunks.ContainsKey(key.Index);

    private static bool HasFire(GasOverlayChunk chunk)
    {
        var tiles = new GasChunkEnumerator(chunk);
        while (tiles.MoveNext(out var gas))
        {
            if (gas.FireState != 0)
                return true;
        }

        return false;
    }

    private static float Progress(float now, float start) =>
        Math.Clamp((now - start) / OrbitraGasField.FadeSeconds, 0f, 1f);

    public void Dispose() => _cache.Dispose();
}
