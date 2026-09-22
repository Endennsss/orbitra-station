using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._Orbitra.Particles;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Particles;

public sealed partial class OrbitraParticleSystem
{
    // Фоновая пыль: ограниченный выбор ламп и таймеры без накопления за экраном.
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    private static readonly ProtoId<OrbitraParticleEffectPrototype> AmbientDustEffect = "OrbitraParticleAmbientDust";

    private readonly OverlayResourceCache<AmbientViewport> _ambientViewports = new();
    private readonly HashSet<Entity<PointLightComponent>> _ambientCandidates = new();
    private readonly Dictionary<EntityUid, (TimeSpan Next, TimeSpan Seen)> _ambientNext = new();
    private readonly HashSet<EntityUid> _ambientSelected = new();
    private readonly List<EntityUid> _ambientExpired = new();

    internal void CollectAmbient(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye != null)
            CollectAmbient(args.Viewport, args.MapId, args.WorldAABB);
    }

    internal void CollectAmbient(IClydeViewport viewport, MapId mapId, Box2 bounds, TimeSpan? time = null)
    {
        var (lampLimit, moteLimit) = OrbitraDust.AmbientBudget(Pool.Capacity);
        if (lampLimit == 0)
            return;
        var cache = _ambientViewports.GetForViewport(viewport, static _ => new AmbientViewport());
        var now = time ?? _timing.RealTime;
        if (now >= cache.Refresh)
        {
            cache.Refresh = now + TimeSpan.FromSeconds(0.5);
            cache.Lamps.Clear();
            _ambientCandidates.Clear();
            _lookup.GetEntitiesIntersecting(mapId, bounds, _ambientCandidates);
            foreach (var light in _ambientCandidates)
            {
                if (!CanAmbient(light, out var grid, out var origin))
                    continue;
                var world = _transform.ToMapCoordinates(new EntityCoordinates(grid, origin));
                cache.Lamps.Add((light.Owner, Vector2.DistanceSquared(world.Position, bounds.Center)));
            }
            cache.Lamps.Sort(static (a, b) =>
            {
                var distance = a.Distance.CompareTo(b.Distance);
                return distance != 0 ? distance : a.Uid.CompareTo(b.Uid);
            });
            if (cache.Lamps.Count > lampLimit)
                cache.Lamps.RemoveRange(lampLimit, cache.Lamps.Count - lampLimit);
        }

        foreach (var (uid, _) in cache.Lamps)
        {
            if (!_ambientSelected.Contains(uid) && _ambientSelected.Count >= lampLimit)
                continue;
            _ambientSelected.Add(uid);
            if (!_ambientNext.TryGetValue(uid, out var timer) || now - timer.Seen > TimeSpan.FromSeconds(0.75))
            {
                _ambientNext[uid] = (now + TimeSpan.FromSeconds((1.5 + _random.NextDouble() * 3) / _density), now);
                continue;
            }
            _ambientNext[uid] = (timer.Next, now);
            if (now < timer.Next)
                continue;
            // Не накапливаем пропущенные выбросы и не догоняем зависший кадр.
            _ambientNext[uid] = (now + TimeSpan.FromSeconds((2.5 + _random.NextDouble()) / _density), now);
            if (Pool.AmbientCount() >= moteLimit || !TryComp<PointLightComponent>(uid, out var light) ||
                !CanAmbient((uid, light), out var grid, out var origin))
                continue;
            TryAmbientParticle(uid, grid, origin, bounds);
        }
    }

    internal bool CanAmbient(Entity<PointLightComponent> light, out EntityUid grid, out Vector2 origin)
    {
        grid = default;
        origin = default;
        if (light.Comp.Deleted || !light.Comp.Enabled || light.Comp.Energy <= 0 || light.Comp.ContainerOccluded ||
            light.Comp.MaskAutoRotate || !TryComp<TransformComponent>(light, out var xform) || !xform.Anchored ||
            xform.GridUid is not { } gridUid || !TryComp<OrbitraAmbientDustGridComponent>(gridUid, out var zones) ||
            zones.Regions.Count == 0 || !TryComp<SpriteComponent>(light, out var sprite) || !sprite.Visible ||
            sprite.ContainerOccluded || sprite.Color.A <= 0 || (MetaData(light).Flags & MetaDataFlags.Detached) != 0)
            return false;
        if (TryComp<PoweredLightComponent>(light, out var powered) && !powered.CurrentLit)
            return false;
        // Слой штатного светильника исключает экраны, фонарики и служебные PointLight.
        if (!_breathSprites.LayerMapTryGet((light.Owner, sprite), PoweredLightLayers.Glow, out var layer, false) ||
            !sprite[layer].Visible || sprite[layer].Color.A <= 0)
            return false;
        grid = gridUid;
        var world = Vector2.Transform(light.Comp.Offset, _transform.GetWorldMatrix(light.Owner));
        origin = _transform.ToCoordinates(grid, new MapCoordinates(world, xform.MapID)).Position;
        // Лампа может стоять за границей, но область испускания должна пересекать зону.
        foreach (var region in zones.Regions)
        {
            if (region.Enlarged(1.25f).Contains(origin))
                return true;
        }
        return false;
    }

    private bool TryAmbientParticle(EntityUid lamp, EntityUid grid, Vector2 origin, Box2 viewport)
    {
        if (!TryComp<MapGridComponent>(grid, out var gridComp) ||
            !TryComp<OrbitraAmbientDustGridComponent>(grid, out var zones) ||
            !_prototypes.TryIndex(AmbientDustEffect, out var effect))
            return false;
        var lightMap = _transform.ToMapCoordinates(new EntityCoordinates(grid, origin));
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var angle = (float) _random.NextDouble() * MathF.Tau;
            var radius = MathF.Sqrt((float) _random.NextDouble()) * 1.25f;
            var point = origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var coordinates = new EntityCoordinates(grid, point);
            if (!OrbitraDust.Contains(zones.Regions, point) || _map.GetTileRef(grid, gridComp, coordinates).Tile.IsEmpty)
                continue;
            var mapPoint = _transform.ToMapCoordinates(coordinates);
            if (!viewport.Contains(mapPoint.Position))
                continue;
            var delta = mapPoint.Position - lightMap.Position;
            var blocked = false;
            if (delta.LengthSquared() > 0.0001f)
            {
                var ray = new CollisionRay(lightMap.Position, Vector2.Normalize(delta), (int) (CollisionGroup.Opaque | CollisionGroup.Impassable));
                foreach (var _ in _physics.IntersectRay(lightMap.MapId, ray, delta.Length(), lamp))
                {
                    blocked = true;
                    break;
                }
            }
            if (blocked)
                continue;
            var drift = (float) _random.NextDouble() * MathF.Tau;
            var speed = 0.02f + (float) _random.NextDouble() * 0.04f;
            return Pool.Add(new OrbitraParticlePool.Particle
            {
                Parent = grid,
                Position = point,
                Origin = origin,
                Velocity = new Vector2(MathF.Cos(drift), MathF.Sin(drift)) * speed,
                Lifetime = 3f + (float) _random.NextDouble() * 2f,
                Size = (0.5f + (float) _random.NextDouble() * 0.5f) / 32f,
                Effect = effect,
            });
        }
        return false;
    }

    private void PruneAmbient()
    {
        _ambientExpired.Clear();
        foreach (var (uid, timer) in _ambientNext)
        {
            // Пропущенный draw не должен каждый раз сбрасывать многосекундное ожидание.
            if (_timing.RealTime - timer.Seen > TimeSpan.FromSeconds(0.75))
                _ambientExpired.Add(uid);
        }
        foreach (var uid in _ambientExpired)
            _ambientNext.Remove(uid);
        _ambientSelected.Clear();
    }

    private void ClearAmbient()
    {
        _ambientViewports.Dispose();
        _ambientNext.Clear();
        _ambientSelected.Clear();
        _ambientCandidates.Clear();
    }

    private sealed class AmbientViewport : IDisposable
    {
        public TimeSpan Refresh;
        public readonly List<(EntityUid Uid, float Distance)> Lamps = new();
        public void Dispose() => Lamps.Clear();
    }
}
