using System.Numerics;
using Content.Shared._Orbitra.Particles;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Utility;
using Robust.Shared.Containers;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Particles;

public sealed partial class OrbitraParticleSystem
{
    [Dependency] private SharedOrbitraColdBreathSystem _coldBreath = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SpriteSystem _breathSprites = default!;
    private EntityQuery<OrbitraColdBreathComponent> _breathQuery;
    private EntityQuery<OrbitraColdBreathVisualsComponent> _breathVisualQuery;
    private EntityQuery<HumanoidProfileComponent> _humanoidQuery;
    private readonly Dictionary<EntityUid, BreathClock> _breaths = new();
    private readonly List<EntityUid> _staleBreaths = new();
    private static readonly ProtoId<OrbitraParticleEffectPrototype> BreathEffect = "OrbitraParticleBreath";

    private struct BreathClock
    {
        public TimeSpan LastSeen;
        public TimeSpan Next;
    }

    private void InitializeBreath()
    {
        _breathQuery = GetEntityQuery<OrbitraColdBreathComponent>();
        _breathVisualQuery = GetEntityQuery<OrbitraColdBreathVisualsComponent>();
        _humanoidQuery = GetEntityQuery<HumanoidProfileComponent>();
    }

    private void PruneBreath()
    {
        _staleBreaths.Clear();
        foreach (var (uid, clock) in _breaths)
        {
            if (_timing.RealTime - clock.LastSeen > TimeSpan.FromSeconds(0.5))
                _staleBreaths.Add(uid);
        }
        foreach (var uid in _staleBreaths)
            _breaths.Remove(uid);
    }

    internal void CollectBreath(Entity<SpriteComponent> ent, Angle eyeRotation)
    {
        if (!_breathQuery.TryComp(ent, out var breath) || breath.Intensity == 0 || Pool.Capacity == 0)
            return;
        // Инвентарь и контейнеры проверяются в TryExhale, а не на каждом кадре.
        var now = _timing.RealTime;
        if (!_breaths.TryGetValue(ent, out var clock))
        {
            // Вход в viewport не воспроизводит пропущенные выдохи.
            _breaths[ent] = new BreathClock { LastSeen = now, Next = now + NextBreathDelay() };
            return;
        }
        clock.LastSeen = now;
        if (now < clock.Next)
        {
            _breaths[ent] = clock;
            return;
        }
        clock.Next = now + NextBreathDelay();
        _breaths[ent] = clock;
        TryExhale(ent, breath.Intensity, eyeRotation);
    }

    private TimeSpan NextBreathDelay() => TimeSpan.FromSeconds(2.2 + _random.NextDouble() * 0.6);

    internal bool CanExhale(Entity<SpriteComponent> ent) =>
        Pool.Capacity > 0 && !ent.Comp.Deleted && ent.Comp.Visible && !ent.Comp.ContainerOccluded && ent.Comp.Color.A > 0 &&
        (MetaData(ent).Flags & MetaDataFlags.Detached) == 0 && _mobState.IsAlive(ent) &&
        !_containers.IsEntityInContainer(ent) && !_coldBreath.IsMouthCovered(ent);

    internal bool TryExhale(Entity<SpriteComponent> ent, byte intensity, Angle eyeRotation)
    {
        if (intensity == 0 || !CanExhale(ent) || !_transformQuery.TryComp(ent, out var xform) ||
            xform.MapUid is not { } map || !_prototypes.TryIndex(BreathEffect, out var effect))
            return false;
        var sprite = ent.Comp;
        var humanoid = _humanoidQuery.HasComp(ent);
        if (!TryGetBreathLayer(ent, humanoid, out var layer))
            return false;
        var (worldPosition, worldRotation) = _transform.GetWorldPositionRotation(ent);
        var angle = worldRotation + eyeRotation;
        {
            var direction = SpriteComponent.Layer.GetDirection(layer.ActualState?.RsiDirections ?? RsiDirectionType.Dir4, angle);
            layer.GetLayerDrawMatrix(direction, out var layerMatrix);
            if (sprite.EnableDirectionOverride && layer.ActualState is { } state)
                direction = sprite.DirectionOverride.Convert(state.RsiDirections);
            direction = direction.OffsetRsiDir(layer.DirOffset);
            // Компактный выдох с обратной стороны лица закрыт самой головой.
            if (!IsBreathFacingVisible(direction, humanoid))
                return false;
            var renderRotation = sprite.NoRotation ? -eyeRotation :
                sprite.SnapCardinals ? worldRotation - angle.RoundToCardinalAngle() : worldRotation;
            if (sprite.GranularLayersRendering)
            {
                renderRotation = layer.RenderingStrategy switch
                {
                    LayerRenderingStrategy.Default => worldRotation,
                    LayerRenderingStrategy.NoRotation => -eyeRotation,
                    LayerRenderingStrategy.SnapToCardinals => worldRotation - angle.RoundToCardinalAngle(),
                    _ => renderRotation,
                };
            }
            _breathVisualQuery.TryComp(ent, out var visual);
            var mouth = GetMouth(direction, humanoid, visual, (Vector2) layer.PixelSize / 32f);
            var matrix = layerMatrix * sprite.LocalMatrix * Matrix3Helpers.CreateTransform(worldPosition, renderRotation);
            var position = Vector2.Transform(mouth, matrix);
            var forward = GetBreathForward(direction);
            var velocity = Vector2.TransformNormal(forward * effect.Speed * 0.9f, matrix) + (-eyeRotation).RotateVec(Vector2.UnitY * effect.Speed * 0.4f);
            var visualScale = Math.Clamp(Vector2.TransformNormal(Vector2.UnitX, matrix).Length(), 0.25f, 3f);
            var parent = xform.GridUid ?? map;
            var point = _transform.ToCoordinates(parent, new MapCoordinates(position, xform.MapID));
            var parentRotation = _transform.GetWorldRotation(parent);
            velocity = (-parentRotation).RotateVec(velocity);
            var count = Math.Clamp((int) MathF.Ceiling(Math.Max(0, effect.Count - _random.Next(2)) * _density), 0, 32);
            var opacity = Math.Clamp(intensity / 15f, 0f, 1f) * sprite.Color.A * layer.Color.A;
            for (var i = 0; i < count; i++)
            {
                Pool.Add(new OrbitraParticlePool.Particle
                {
                    Parent = parent,
                    Position = point.Position + new Vector2((float) _random.NextDouble() - 0.5f, (float) _random.NextDouble() - 0.5f) * effect.SpawnRadius * 2 * visualScale,
                    Origin = point.Position,
                    Velocity = velocity * (0.8f + (float) _random.NextDouble() * 0.4f),
                    Lifetime = effect.Lifetime * (0.625f + (float) _random.NextDouble() * 0.375f),
                    Size = effect.Size * (0.78f + (float) _random.NextDouble() * 0.33f) * visualScale,
                    Effect = effect,
                    Opacity = opacity,
                });
            }
            return true;
        }
    }

    internal bool TryGetBreathLayer(Entity<SpriteComponent> ent, bool humanoid,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SpriteComponent.Layer? layer)
    {
        layer = null;
        if (humanoid)
            return _breathSprites.TryGetLayer(ent.AsNullable(), HumanoidVisualLayers.Head, out layer, false) && IsBreathLayer(layer);
        if (_breathSprites.TryGetLayer(ent.AsNullable(), Content.Client.DamageState.DamageStateVisualLayers.Base, out layer, false) && IsBreathLayer(layer))
            return true;
        foreach (var candidate in ent.Comp.AllLayers)
        {
            if (candidate is not SpriteComponent.Layer actual || !IsBreathLayer(actual))
                continue;
            layer = actual;
            return true;
        }
        layer = null;
        return false;
    }

    private static bool IsBreathLayer(SpriteComponent.Layer layer) =>
        layer.Visible && !layer.Blank && layer.Color.A > 0 && layer.PixelSize != Vector2i.Zero && layer.CopyToShaderParameters == null;

    internal static bool IsBreathFacingVisible(RsiDirection direction, bool humanoid) =>
        !humanoid || direction != RsiDirection.North;

    internal static Vector2 GetBreathForward(RsiDirection direction) => direction switch
    {
        RsiDirection.North => Vector2.UnitY,
        RsiDirection.East => Vector2.UnitX,
        RsiDirection.West => -Vector2.UnitX,
        RsiDirection.NorthEast => Vector2.Normalize(new Vector2(1, 1)),
        RsiDirection.NorthWest => Vector2.Normalize(new Vector2(-1, 1)),
        RsiDirection.SouthEast => Vector2.Normalize(new Vector2(1, -1)),
        RsiDirection.SouthWest => Vector2.Normalize(new Vector2(-1, -1)),
        _ => -Vector2.UnitY,
    };

    internal static Vector2 GetMouth(RsiDirection direction, bool humanoid, OrbitraColdBreathVisualsComponent? visual, Vector2 size)
    {
        // Диагональные кадры используют промежуточную точку, а не южную по умолчанию.
        if (direction is RsiDirection.NorthEast or RsiDirection.NorthWest or RsiDirection.SouthEast or RsiDirection.SouthWest)
        {
            var vertical = direction is RsiDirection.NorthEast or RsiDirection.NorthWest ? RsiDirection.North : RsiDirection.South;
            var horizontal = direction is RsiDirection.NorthEast or RsiDirection.SouthEast ? RsiDirection.East : RsiDirection.West;
            return (GetMouth(vertical, humanoid, visual, size) + GetMouth(horizontal, humanoid, visual, size)) / 2;
        }
        if (visual != null)
            return direction switch
            {
                RsiDirection.North => visual.North,
                RsiDirection.East => visual.East,
                RsiDirection.West => visual.West,
                _ => visual.South,
            };
        if (humanoid)
            return direction switch
            {
                RsiDirection.North => new Vector2(0, 0.25f),
                RsiDirection.East => new Vector2(0.13f, 0.18f),
                RsiDirection.West => new Vector2(-0.13f, 0.18f),
                _ => new Vector2(0, 0.15f),
            };
        return direction switch
        {
            RsiDirection.North => new Vector2(0, size.Y * 0.3f),
            RsiDirection.East => new Vector2(size.X * 0.3f, 0),
            RsiDirection.West => new Vector2(-size.X * 0.3f, 0),
            _ => new Vector2(0, -size.Y * 0.3f),
        };
    }
}
