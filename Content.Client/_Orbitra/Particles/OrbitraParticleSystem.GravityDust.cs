using System.Numerics;
using Content.Shared._Orbitra.Particles;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.Gravity;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Particles;

public sealed partial class OrbitraParticleSystem
{
    private static readonly ProtoId<OrbitraParticleEffectPrototype> GravityDustEffect = "OrbitraParticleGravityDust";
    private static readonly TimeSpan GravityDustSampleInterval = TimeSpan.FromSeconds(0.25);

    [Dependency] private SharedMapSystem _maps = default!;

    private List<Entity<MapGridComponent>> _gravityDustGrids = new();
    private readonly List<Vector2i> _gravityDustTiles = new(192);
    private TimeSpan _nextGravityDustSample;
    private bool _hasGravityDust;

    internal void CollectGravityDust(MapId map, Box2 bounds)
    {
        var now = _timing.RealTime;
        if (now < _nextGravityDustSample || map == MapId.Nullspace || _configuration.GetCVar(CCVars.ReducedMotion))
            return;

        _nextGravityDustSample = now + GravityDustSampleInterval;
        var remaining = Math.Min(64, Pool.Capacity / 8);
        var keepBounds = bounds.Enlarged(1.5f);
        for (var i = Pool.Count - 1; i >= 0; i--)
        {
            ref var dust = ref Pool.Particles[i];
            if (!dust.GravityDustLift.HasValue)
                continue;

            if (!_transformQuery.TryComp(dust.Parent, out var parent) || parent.MapID != map ||
                !keepBounds.Contains(Vector2.Transform(dust.Position, _transform.GetWorldMatrix(parent))))
                Pool.RemoveAt(i);
            else
                remaining--;
        }

        if (remaining <= 0 || Pool.Count >= Pool.Capacity || !_prototypes.TryIndex(GravityDustEffect, out var effect))
            return;

        _gravityDustGrids.Clear();
        _maps.FindGridsIntersecting(map, bounds, ref _gravityDustGrids);
        foreach (var grid in _gravityDustGrids)
        {
            if (!_transformQuery.TryComp(grid, out var xform) || GravityDustHasGravity(grid, xform.MapUid) ||
                !TryComp<GasTileOverlayComponent>(grid, out var gas))
                continue;

            _gravityDustTiles.Clear();
            var tiles = _maps.GetTilesIntersecting(grid, grid.Comp, bounds);
            while (_gravityDustTiles.Count < 192 && tiles.MoveNext(out var tile))
            {
                if (IsGravityDustFloor(gas, tile.GridIndices))
                    _gravityDustTiles.Add(tile.GridIndices);
            }

            var count = Math.Min(remaining, Math.Min(_gravityDustTiles.Count * 2, Pool.Capacity - Pool.Count));
            for (var i = 0; i < count; i++)
            {
                var tile = _gravityDustTiles[_random.Next(_gravityDustTiles.Count)];
                var offset = new Vector2(_random.NextSingle() - 0.5f, _random.NextSingle() - 0.5f);
                var point = _maps.GridTileToLocal(grid, grid.Comp, tile).Position + offset * (grid.Comp.TileSize * 0.72f);
                var angle = _random.NextSingle() * MathF.Tau;
                var speed = effect.Speed * (0.45f + _random.NextSingle() * 0.55f);
                Pool.Add(new OrbitraParticlePool.Particle
                {
                    Parent = grid,
                    Position = point,
                    Origin = point,
                    Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed,
                    Lifetime = float.PositiveInfinity,
                    Size = effect.Size * (0.60f + _random.NextSingle() * 0.75f),
                    Effect = effect,
                    Opacity = 0f,
                    GravityDustLift = 0f,
                });
            }

            _hasGravityDust |= count > 0;
            remaining -= count;
            if (remaining <= 0 || Pool.Count >= Pool.Capacity)
                break;
        }
    }

    private void UpdateGravityDust(float frameTime)
    {
        if (!_hasGravityDust || !float.IsFinite(frameTime) || frameTime <= 0f)
            return;

        var dt = Math.Min(frameTime, 0.05f);
        var reducedMotion = _configuration.GetCVar(CCVars.ReducedMotion);
        _hasGravityDust = false;
        for (var i = Pool.Count - 1; i >= 0; i--)
        {
            ref var dust = ref Pool.Particles[i];
            if (dust.GravityDustLift is not { } lift)
                continue;

            if (reducedMotion || !_transformQuery.TryComp(dust.Parent, out var xform) ||
                !TryComp<MapGridComponent>(dust.Parent, out var grid) ||
                !TryComp<GasTileOverlayComponent>(dust.Parent, out var gas) ||
                !IsGravityDustFloor(gas, (dust.Position / grid.TileSize).Floored()))
            {
                Pool.RemoveAt(i);
                continue;
            }

            var settling = GravityDustHasGravity(dust.Parent, xform.MapUid);
            var nextLift = Math.Clamp(lift + dt * (settling ? -1.55f : 1.15f), 0f, 1f);
            if (settling && nextLift <= 0f)
            {
                Pool.RemoveAt(i);
                continue;
            }

            dust.Age += dt;
            var sway = new Vector2(-dust.Velocity.Y, dust.Velocity.X) *
                       (MathF.Sin(dust.Age * 1.7f + dust.Origin.X) * 0.40f);
            // Высота над полом читается как небольшой сдвиг в плоскости тайла.
            var drift = (dust.Velocity + sway) * (settling ? nextLift * 0.22f : 0.5f + nextLift * 0.5f);
            var vertical = (nextLift - lift) * dust.Size * 6f;
            var position = dust.Position + drift * dt + new Vector2(0f, vertical);
            if (IsGravityDustFloor(gas, (position / grid.TileSize).Floored()))
                dust.Position = position;
            else
                dust.Velocity = -dust.Velocity;

            dust.GravityDustLift = nextLift;
            dust.Opacity = nextLift * (0.76f + 0.24f * MathF.Sin(dust.Age * 1.4f + dust.Origin.Y));
            _hasGravityDust = true;
        }
    }

    private bool GravityDustHasGravity(EntityUid grid, EntityUid? map) =>
        TryComp<GravityComponent>(grid, out var gravity) && gravity.Enabled ||
        TryComp<GravityComponent>(map, out var mapGravity) && mapGravity.Enabled;

    private static bool IsGravityDustFloor(GasTileOverlayComponent gas, Vector2i tile)
    {
        if (!gas.Chunks.TryGetValue(SharedGasTileOverlaySystem.GetGasChunkIndices(tile), out var chunk))
            return false;

        var offset = tile - chunk.Origin;
        var data = chunk.TileData[offset.X + offset.Y * SharedGasTileOverlaySystem.ChunkSize];
        return data.Opacity != null && data.ByteGasTemperature.Value <= ThermalByte.TempResolution;
    }
}
