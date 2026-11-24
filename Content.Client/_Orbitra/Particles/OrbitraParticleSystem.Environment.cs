using System.Numerics;
using Content.Client.Shuttles;
using Content.Shared.CCVar;
using Content.Shared.Decals;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Content.Shared.Movement.Components;
using Content.Shared.Shuttles.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Client._Orbitra.Particles;

public sealed partial class OrbitraParticleSystem
{
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private ChunkEntitySystem _decalChunks = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SpriteSystem _sprites = default!;

    private static readonly TimeSpan RunningDustSampleInterval = TimeSpan.FromSeconds(0.05);
    private static readonly TimeSpan RunningDustSampleTimeout = TimeSpan.FromSeconds(0.25);
    private readonly Dictionary<EntityUid, RunningDustSample> _runningDust = new();

    private readonly record struct RunningDustSample(EntityCoordinates Point, TimeSpan Time, float Distance);

    private void CollectEnvironment(EntityUid uid, AppearanceComponent? appearance, TransformComponent xform)
    {
        if (HasComp<ThrusterComponent>(uid) && appearance != null &&
            _appearance.TryGetData<bool>(uid, ThrusterVisualState.State, out var active, appearance) && active &&
            _appearance.TryGetData<bool>(uid, ThrusterVisualState.Thrusting, out var thrusting, appearance) && thrusting)
        {
            if (_spriteQuery.TryComp(uid, out var sprite) &&
                _sprites.TryGetLayer((uid, sprite), ThrusterVisualLayers.ThrustingUnshaded, out var exhaust, false) &&
                exhaust.Offset.Y > 0)
            {
                var rotation = _transform.GetWorldRotation(xform);
                var offset = sprite.Offset + exhaust.Offset + new Vector2(0, 0.24f);
                var parent = xform.GridUid ?? xform.MapUid!.Value;
                var point = _transform.ToCoordinates(parent, _transform.GetMapCoordinates(xform).Offset(rotation.RotateVec(offset)));
                Continuous((uid, -3), point, "OrbitraParticleThrusterPulse", 12f, (float) (rotation.Theta + Math.PI / 2));
            }
            return;
        }

        var extinguisher = MetaData(uid).EntityPrototype?.ID == "ExtinguisherSpray";
        if (xform.GridUid is not { } grid || (!extinguisher && !HasComp<MobMoverComponent>(uid)) ||
            !TryComp<PhysicsComponent>(uid, out var body))
            return;

        if (extinguisher)
        {
            var velocity = body.LinearVelocity;
            if (velocity.LengthSquared() < 0.09f)
                return;
            var point = _transform.ToCoordinates(grid, _transform.GetMapCoordinates(xform));
            Continuous((uid, -4), point, "OrbitraParticleExtinguisherFan", 30f,
                (float) (_transform.GetWorldRotation(grid).Theta + MathF.Atan2(velocity.Y, velocity.X)));
            return;
        }

        if (body.BodyStatus != BodyStatus.OnGround || _configuration.GetCVar(CCVars.ReducedMotion))
        {
            _runningDust.Remove(uid);
            return;
        }

        CollectRunningDust(uid, xform, grid);
    }

    private void CollectRunningDust(EntityUid uid, TransformComponent xform, EntityUid grid)
    {
        var now = _timing.RealTime;
        var hasSample = _runningDust.TryGetValue(uid, out var previous);
        var elapsed = now - previous.Time;
        if (hasSample && elapsed < RunningDustSampleInterval)
            return;

        // Интерполированные координаты дают длину шага и у персонажей других игроков.
        var point = xform.ParentUid == grid
            ? xform.Coordinates
            : _transform.ToCoordinates(grid, _transform.GetMapCoordinates(xform));
        _runningDust[uid] = new RunningDustSample(point, now, 0);
        if (!hasSample || previous.Point.EntityId != grid || elapsed > RunningDustSampleTimeout)
            return;

        var displacement = point.Position - previous.Point.Position;
        var distance = displacement.Length();
        var seconds = (float) elapsed.TotalSeconds;
        if (distance < 3f * seconds || distance > 16f * seconds)
            return;

        const float stride = 0.55f;
        var traveled = previous.Distance + distance;
        if (traveled < stride)
        {
            _runningDust[uid] = new RunningDustSample(point, now, traveled);
            return;
        }
        _runningDust[uid] = new RunningDustSample(point, now, traveled % stride);
        if (_gravity.IsWeightless(uid) || !_turf.TryGetTileRef(point, out var tile))
            return;

        var effect = _turf.GetContentTileDefinition(tile.Value).ID switch
        {
            "FloorDirt" or "FloorPlanetDirt" => "OrbitraParticleRunningDirt",
            "FloorAsteroidSand" or "FloorAsteroidSandBorderless" or "FloorAstroAsteroidSand" or
                "FloorAstroAsteroidSandBorderless" or "FloorDesertAstroSand" => "OrbitraParticleRunningSand",
            _ => HasAsh(point) ? "OrbitraParticleRunningAsh" : null,
        };
        if (effect == null)
            return;

        var back = -displacement / distance;
        var feet = point.Offset(back * 0.28f - new Vector2(0, 0.25f));
        Emit(feet, effect, (int) MathF.Ceiling(3f * _density),
            (float) (_transform.GetWorldRotation(grid).Theta + MathF.Atan2(back.Y, back.X)), true);
    }

    private bool HasAsh(EntityCoordinates position)
    {
        var bounds = Box2.CenteredAround(position.Position, new Vector2(1.2f));
        foreach (var chunk in _decalChunks.GetChunksIntersecting(position.EntityId, bounds, GetEntityQuery<DecalChunkComponent>()))
        {
            foreach (var decal in chunk.Comp2.Decals.Values)
            {
                if (decal.Id is "burnt1" or "burnt2" or "burnt3" or "burnt4" &&
                    Vector2.DistanceSquared(decal.Coordinates + new Vector2(0.5f), position.Position) <= 0.36f)
                    return true;
            }
        }

        return false;
    }
}
