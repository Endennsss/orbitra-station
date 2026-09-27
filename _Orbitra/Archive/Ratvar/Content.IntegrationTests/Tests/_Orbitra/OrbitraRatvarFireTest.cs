using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Checks native ignition and damage conversion without requiring the wielder to be a cultist.</summary>
[TestFixture]
public sealed class OrbitraRatvarFireTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(true, -5)]
    public async Task FireOnlyAffectsHitMobsOnce(bool actualHit, int initialStacks)
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default, user = default, prop = default;
        float initialUserStacks = 0;
        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            target = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            prop = SEntMan.SpawnEntity("ChairWood", map.GridCoords);
            var weapon = SEntMan.SpawnEntity("OrbitraRatvarSpear", map.GridCoords);
            var blessing = SEntMan.AddComponent<OrbitraRatvarEnchantmentComponent>(weapon);
            blessing.Kind = OrbitraRatvarEnchantment.Burn;
            blessing.Level = 3;
            blessing.FireStacks = 3;
            Server.System<FlammableSystem>().SetFireStacks(target, initialStacks);
            initialUserStacks = SEntMan.GetComponent<FlammableComponent>(user).FireStacks;
            var hit = new MeleeHitEvent([target, target, user, prop], user, weapon, new DamageSpecifier(), null)
            {
                IsHit = actualHit,
            };
            SEntMan.EventBus.RaiseLocalEvent(weapon, hit);
        });
        await Server.WaitAssertion(() =>
        {
            var fire = SEntMan.GetComponent<FlammableComponent>(target);
            Assert.That(fire.FireStacks, Is.EqualTo(initialStacks + (actualHit ? 3 : 0)).Within(0.11f));
            Assert.That(fire.OnFire, Is.EqualTo(actualHit && initialStacks >= 0));
            Assert.That(SEntMan.GetComponent<FlammableComponent>(user).FireStacks, Is.EqualTo(initialUserStacks));
            if (SEntMan.TryGetComponent<FlammableComponent>(prop, out var propFire))
                Assert.That(propFire.OnFire, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task HeatConversionPreservesDamageAndWieldBonus(bool wield)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, weapon = default;
        DamageSpecifier original = default!;
        var wielded = false;
        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            weapon = SEntMan.SpawnEntity("OrbitraRatvarSpear", map.GridCoords);
            Server.System<SharedHandsSystem>().TryPickup(user, weapon);
            if (wield)
                wielded = Server.System<SharedWieldableSystem>().TryWield((weapon, null), user);
            original = Server.System<SharedMeleeWeaponSystem>().GetDamage(weapon, user);
            var blessing = SEntMan.AddComponent<OrbitraRatvarEnchantmentComponent>(weapon);
            blessing.Kind = OrbitraRatvarEnchantment.Burn;
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(wielded, Is.EqualTo(wield));
            var result = Server.System<SharedMeleeWeaponSystem>().GetDamage(weapon, user);
            Assert.That(result.DamageDict.Keys, Is.EquivalentTo(new[] { "Heat" }));
            Assert.That(result.GetTotal(), Is.EqualTo(original.GetTotal()));
            Assert.That(SEntMan.GetComponent<MeleeWeaponComponent>(weapon).Damage.DamageDict.ContainsKey("Piercing"), Is.True);
        });
    }
}
