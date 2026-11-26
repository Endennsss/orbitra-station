using Content.Server._Orbitra.Surgery;

namespace Content.Server.Projectiles;

public sealed partial class ProjectileSystem
{
    [Dependency] private OrbitraTraumaSystem _orbitraTrauma = default!;
}
