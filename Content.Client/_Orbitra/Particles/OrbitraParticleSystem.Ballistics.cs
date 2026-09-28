using Content.Shared._Orbitra.Particles;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Particles;

public sealed partial class OrbitraParticleSystem
{
    private static readonly ProtoId<OrbitraParticleEffectPrototype> BulletMarkEffect = "OrbitraParticleBulletMark";
    private void TryImpactMark(EntityUid source, EntityCoordinates point)
    {
        if (!_transformQuery.TryComp(source, out var xform) || !xform.Anchored || xform.GridUid != point.EntityId ||
            !_prototypes.TryIndex(BulletMarkEffect, out var effect))
            return;
        Pool.Add(new()
        {
            Parent = point.EntityId,
            Position = point.Position,
            Origin = point.Position,
            Effect = effect,
            ImpactSource = source,
            Lifetime = 15f + (float) _random.NextDouble() * 10f,
            Size = effect.Size,
        });
    }

    private void PruneImpactMarks()
    {
        for (var i = Pool.Count - 1; i >= 0; i--)
        {
            ref var particle = ref Pool.Particles[i];
            if (particle.ImpactSource is not { } source)
                continue;
            if (TerminatingOrDeleted(source) || !_transformQuery.TryComp(source, out var xform) ||
                !xform.Anchored || xform.GridUid != particle.Parent ||
                !_spriteQuery.TryComp(source, out var sprite) || !sprite.Visible || sprite.ContainerOccluded || sprite.Color.A <= 0 ||
                (MetaData(source).Flags & MetaDataFlags.Detached) != 0)
                Pool.RemoveAt(i);
        }
    }
}
