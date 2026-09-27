using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Wieldable;
using Content.Shared.Damage;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>Applies the melee bonus on both the server and predicted client.</summary>
public sealed class OrbitraRatvarEnchantmentSystem : EntitySystem
{
    public override void Initialize() =>
        SubscribeLocalEvent<OrbitraRatvarEnchantmentComponent, GetMeleeDamageEvent>(OnDamage,
            after: [typeof(SharedWieldableSystem), typeof(SharedMeleeWeaponSystem)]);

    private void OnDamage(Entity<OrbitraRatvarEnchantmentComponent> ent, ref GetMeleeDamageEvent args)
    {
        args.Damage += ent.Comp.Bonus;
        if (ent.Comp.Kind != OrbitraRatvarEnchantment.Burn)
            return;

        // Переводим в ожоги также прибавку от двуручного хвата, не меняя исходный прототип.
        var total = args.Damage.GetTotal();
        args.Damage = new DamageSpecifier();
        args.Damage.DamageDict.Add("Heat", total);
    }
}
