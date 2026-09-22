using Content.Server._Orbitra.Particles;

namespace Content.Server.Projectiles;

public sealed partial class ProjectileSystem
{
    [Dependency] private OrbitraParticleBurstSystem _orbitraParticles = default!;

}
