using Content.Client._Orbitra.Particles;
using Content.Shared.Projectiles;

namespace Content.Client.Projectiles;

public sealed partial class ProjectileSystem
{
    [Dependency] private OrbitraParticleSystem _orbitraParticles = default!;

    private bool SkipOrbitraMaterialImpact(ImpactEffectEvent ev) => ev.OrbitraMaterialImpact && _orbitraParticles.Pool.Capacity > 0;
}
