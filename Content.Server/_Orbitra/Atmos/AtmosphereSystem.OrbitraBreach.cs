using System.Numerics;
using Content.Shared._Orbitra.Particles;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio;
using Robust.Shared.Physics;
using Robust.Shared.Player;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    private static readonly SoundSpecifier BreachImpact = new SoundCollectionSpecifier("MetalThud");
    private static readonly TimeSpan BreachCueInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan BreachResetInterval = TimeSpan.FromSeconds(4);

    private bool TryProcessOrbitraBreachFlow(Entity<GridAtmosphereComponent> grid, TileAtmosphere tile)
    {
        if (tile.PressureSpecificTarget is not { Space: true } outlet || tile.PressureDirection == AtmosDirection.Invalid)
            return false;

        if (!tile.AdjacentBits.IsFlagSet(tile.PressureDirection))
            return true;

        // В Monstermos здесь количество воздуха, прошедшего по этому участку, а не давление в кПа.
        var strength = Math.Clamp((tile.PressureDifference - 8f) / 70f, 0f, 1f);
        if (strength <= 0f)
            return true;

        var angle = tile.PressureDirection.ToAngle() + XformSystem.GetWorldRotation(grid);
        RecordOrbitraAirflow(tile);
        if (tile.AdjacentTiles[tile.PressureDirection.ToIndex()] == outlet)
            PlayOrbitraBreachCue(tile, outlet, angle, strength);

        if (!SpaceWind)
            return true;

        var direction = angle.ToWorldVec();
        _entSet.Clear();
        _lookup.GetLocalEntitiesIntersecting(grid.Owner, tile.GridIndices, _entSet, 0f);
        foreach (var uid in _entSet)
        {
            if (!_movedByPressureQuery.TryComp(uid, out var movement) || !movement.Enabled ||
                float.IsPositiveInfinity(movement.MoveResist) ||
                movement.LastHighPressureMovementAirCycle >= grid.Comp.UpdateCounter ||
                !_physicsQuery.TryComp(uid, out var body) || body.BodyType == BodyType.Static ||
                body.Mass <= 0f || Transform(uid).Anchored || _containers.IsEntityInContainer(uid) ||
                TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled)
                continue;

            var mob = HasComp<MobStateComponent>(uid);
            if (mob && strength < 0.72f)
                continue;

            var speed = mob ? 4.5f : 10f * MathF.Sqrt(4f / MathF.Max(4f, body.Mass));
            var targetSpeed = Math.Min(SpaceWindMaxVelocity, speed * strength);
            if (targetSpeed <= 0f)
                continue;

            var speedChange = Math.Clamp(targetSpeed - Vector2.Dot(body.LinearVelocity, direction), 0f, targetSpeed);
            if (speedChange <= 0.01f)
                continue;

            if (mob)
                AddMobMovedByPressure(uid, movement, body);

            _physics.ApplyLinearImpulse(uid, direction * (speedChange * body.Mass), body: body);
            movement.LastHighPressureMovementAirCycle = grid.Comp.UpdateCounter;
        }

        return true;
    }

    private void PlayOrbitraBreachCue(TileAtmosphere tile, TileAtmosphere outlet, Angle angle, float strength)
    {
        var now = _gameTiming.CurTime;
        if (now < outlet.OrbitraNextBreachCue)
            return;

        var first = outlet.OrbitraNextBreachCue == TimeSpan.Zero || now - outlet.OrbitraNextBreachCue > BreachResetInterval;
        outlet.OrbitraNextBreachCue = now + BreachCueInterval;
        var coordinates = _mapSystem.ToCenterCoordinates(tile.GridIndex, tile.GridIndices);
        if (first && strength >= 0.7f)
            _audio.PlayPvs(BreachImpact, coordinates, AudioParams.Default.WithVolume(-6f).WithMaxDistance(12f));
        if (SpaceWindSound is { } wind)
            _audio.PlayPvs(wind, coordinates, wind.Params.WithVolume(-20f + strength * 13f).WithMaxDistance(12f));

        RaiseNetworkEvent(new OrbitraParticleBurstEvent(NetEntity.Invalid, GetNetCoordinates(coordinates),
            "OrbitraParticleBreachDust", (float) (angle.Theta - Math.PI / 2)),
            Filter.Pvs(coordinates, 1f, EntityManager));
    }
}
