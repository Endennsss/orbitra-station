using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Orbitra.Particles;

/// <summary>Shared material and contact geometry helpers; particles themselves are client-only.</summary>
public sealed partial class SharedOrbitraParticleSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public string MaterialEffect(EntityUid target) => OrbitraParticleCVars.MaterialEffect(
        TryComp<OrbitraParticleMaterialComponent>(target, out var material) ? material.Material : OrbitraParticleMaterial.Generic);

    public EntityCoordinates Contact(EntityUid target, EntityUid user, out float direction)
    {
        var targetTransform = Transform(target);
        var point = _transform.GetWorldPosition(targetTransform);
        var worker = _transform.GetWorldPosition(user);
        if (_physics.TryGetNearest(target, new MapCoordinates(worker, targetTransform.MapID), out var nearest, out _))
            point = nearest;
        var delta = worker - point;
        direction = MathF.Atan2(delta.Y, delta.X);
        if (delta.LengthSquared() > 0.0001f)
            point += Vector2.Normalize(delta) * 0.03f;
        return _transform.ToCoordinates(targetTransform.GridUid ?? targetTransform.MapUid!.Value,
            new MapCoordinates(point, targetTransform.MapID));
    }
}
