using Content.Shared._Orbitra.Particles;
using Content.Shared.Damage;

namespace Content.Shared.Weapons.Melee;

public abstract partial class SharedMeleeWeaponSystem
{
    private void RaiseOrbitraParticleHit(EntityUid target, EntityUid user, DamageSpecifier damage)
    {
        // Подтверждённый урон: на клиенте это событие не запускает частицы повторно.
        var ev = new OrbitraParticleMeleeHitEvent(user, damage);
        RaiseLocalEvent(target, ref ev);
    }
}
