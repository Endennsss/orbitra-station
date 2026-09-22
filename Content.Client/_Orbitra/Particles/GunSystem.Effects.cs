using Content.Client._Orbitra.Particles;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private OrbitraGunEffectsSystem _orbitraGunEffects = default!;
}
