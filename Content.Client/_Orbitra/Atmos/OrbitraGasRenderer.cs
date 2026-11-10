using System.Numerics;
using Content.Shared._Orbitra.Graphics;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Atmos;

/// <summary>Draws continuous gas clouds from the existing visible-gas snapshots.</summary>
internal sealed partial class OrbitraGasRenderer : IDisposable
{
    private static readonly ProtoId<ShaderPrototype> ShaderId = "OrbitraSmoothGas";

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private ILogManager _logs = default!;

    private readonly SharedMapSystem _maps;
    private readonly SharedTransformSystem _transform;
    private readonly EntityQuery<GasTileOverlayComponent> _overlays;
    private readonly Vector3[] _palette;
    private readonly Vector4[] _fieldScratch = new Vector4[OrbitraGasField.FieldSize * OrbitraGasField.FieldSize];
    private readonly OrbitraGasCache<ChunkSurface> _cache = new();
    private readonly List<VisibleChunk> _visible = new(OrbitraGasCache<ChunkSurface>.Capacity);
    private readonly HashSet<(EntityUid Grid, Vector2i Index)> _visibleKeys = new();
    private readonly Func<(EntityUid Grid, Vector2i Index), bool> _isVisible;
    private readonly Func<(EntityUid Grid, Vector2i Index), bool> _isAvailable;
    private List<Entity<MapGridComponent>> _grids = new();
    private ShaderPrototype? _prototype;
    private ShaderInstance? _mapShader;
    private MapId _map = MapId.Nullspace;
    private float _time;
    private float _mapChangedAt;
    private Vector4 _mapFrom;
    private Vector4 _mapTo;
    private bool _unavailable;

    private readonly record struct VisibleChunk(EntityUid Grid, GasTileOverlayComponent Overlay, Vector2i Index,
        Matrix3x2 Transform, Box2 Bounds);

    public OrbitraGasRenderer(int[] visibleGasIds)
    {
        IoCManager.InjectDependencies(this);
        _maps = _entities.System<SharedMapSystem>();
        _transform = _entities.System<SharedTransformSystem>();
        _overlays = _entities.GetEntityQuery<GasTileOverlayComponent>();
        _isVisible = _visibleKeys.Contains;
        _isAvailable = IsAvailable;

        // Цвета дымки повторяют прежние спрайты. Цвет в анализаторе задаётся отдельно.
        var atmos = _entities.System<SharedAtmosphereSystem>();
        _palette = new Vector3[visibleGasIds.Length];
        for (var i = 0; i < visibleGasIds.Length; i++)
        {
            var color = (Gas) visibleGasIds[i] switch
            {
                Gas.Plasma => Color.FromHex("#A95FB8"),
                Gas.Tritium => Color.FromHex("#2DDB08"),
                Gas.WaterVapor => Color.FromHex("#F2F8FF"),
                Gas.Ammonia => Color.FromHex("#571E77"),
                Gas.Frezon => Color.FromHex("#6FD1EE"),
                _ => atmos.GetGas(visibleGasIds[i]).Color,
            };
            color = Color.FromSrgb(color);
            _palette[i] = new Vector3(color.R, color.G, color.B);
        }
    }

    public void Advance(float seconds)
    {
        _time += seconds;
        _cache.Retain(_isAvailable);
        if (!_configuration.GetCVar(OrbitraGasCVars.SmoothGases))
            ChangeMap(MapId.Nullspace);
    }

    public bool TryDraw(in OverlayDrawArgs args)
    {
        if (_unavailable || !_configuration.GetCVar(OrbitraGasCVars.SmoothGases))
            return false;

        ChangeMap(args.MapId);
        if (args.MapId == MapId.Nullspace)
            return true;

        var entitiesPass = args.Space == OverlaySpace.WorldSpaceEntities;
        var mapUid = _maps.GetMapOrInvalid(args.MapId);
        var drawMap = _entities.HasComponent<MapGridComponent>(mapUid) == entitiesPass;

        try
        {
            if (entitiesPass && !PrepareChunks(args))
                return false;

            if (drawMap)
            {
                _entities.TryGetComponent<MapAtmosphereComponent>(mapUid, out var atmosphere);
                UpdateMap(atmosphere?.OverlayData.Opacity);
            }
        }
        catch (Exception error)
        {
            _logs.GetSawmill("orbitra.gas").Warning($"Не удалось подготовить дымку, включён штатный оверлей: {error}");
            _unavailable = true;
            Dispose();
            return false;
        }

        var handle = args.WorldHandle;
        var originalTransform = handle.GetTransform();
        var originalShader = handle.GetShader();
        var time = _configuration.GetCVar(CCVars.ReducedMotion) ? 0f : _time;
        try
        {
            if (drawMap && _mapShader != null)
                DrawMap(handle, args.WorldAABB, time);

            if (entitiesPass)
            {
                foreach (var chunk in _visible)
                {
                    var surface = _cache.Entries[(chunk.Grid, chunk.Index)];
                    surface.Draw(handle, chunk.Transform, chunk.Bounds, _time, time);
                }
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
                if ((!_cache.Entries.TryGetValue(key, out var previous) || !previous.MayBeVisible(_time)) && !HasGas(chunk))
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
                _cache.Entries.Add(key, surface = new ChunkSurface(_clyde, GetPrototype()));

            surface.Update(chunk.Overlay.Chunks, chunk.Index, _palette, _fieldScratch, _time);
        }

        return true;
    }

    private void UpdateMap(ReadOnlySpan<byte> opacity)
    {
        var color = OrbitraGasField.Mix(opacity, _palette);
        if (_mapTo == color)
            return;

        _mapFrom = Vector4.Lerp(_mapFrom, _mapTo, Progress(_time, _mapChangedAt));
        _mapTo = color;
        _mapChangedAt = _time;
        if (_mapShader == null)
        {
            _mapShader = GetPrototype().InstanceUnique();
            _mapShader.SetParameter("map_atmosphere", true);
        }
    }

    private void DrawMap(DrawingHandleWorld handle, Box2 bounds, float time)
    {
        var density = Vector4.Lerp(_mapFrom, _mapTo, Progress(_time, _mapChangedAt));
        if (density.W == 0f || _mapShader is not { } shader)
            return;

        shader.SetParameter("map_density", density);
        SetCloudPosition(shader, bounds, time);
        handle.UseShader(shader);
        handle.DrawRect(bounds, Color.White);
    }

    private void ChangeMap(MapId map)
    {
        if (_map == map)
            return;

        _map = map;
        _cache.ChangeMap(map);
        _visible.Clear();
        _visibleKeys.Clear();
        _grids.Clear();
        _mapFrom = _mapTo = default;
    }

    private bool IsAvailable((EntityUid Grid, Vector2i Index) key) =>
        _overlays.TryGetComponent(key.Grid, out var overlay) && overlay.Chunks.ContainsKey(key.Index);

    private static bool HasGas(GasOverlayChunk chunk)
    {
        var tiles = new GasChunkEnumerator(chunk);
        while (tiles.MoveNext(out var gas))
        {
            if (gas.Opacity is not { } opacity)
                continue;

            foreach (var value in opacity)
            {
                if (value != 0)
                    return true;
            }
        }

        return false;
    }

    private ShaderPrototype GetPrototype() => _prototype ??= _prototypes.Index(ShaderId);

    private static float Progress(float now, float start) => Math.Clamp((now - start) / OrbitraGasField.FadeSeconds, 0f, 1f);

    private static void SetCloudPosition(ShaderInstance shader, Box2 bounds, float time)
    {
        shader.SetParameter("cloud_origin", bounds.BottomLeft);
        shader.SetParameter("cloud_size", bounds.Size);
        shader.SetParameter("drift_time", time);
    }

    public void Dispose()
    {
        ChangeMap(MapId.Nullspace);
        _mapShader?.Dispose();
        _mapShader = null;
    }
}
