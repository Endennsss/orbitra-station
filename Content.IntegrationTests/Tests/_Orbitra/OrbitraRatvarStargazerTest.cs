using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraRatvarStargazerTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";
    private static readonly EntProtoId Spear = "OrbitraRatvarSpear";
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [TestCase(OrbitraRatvarEnchantment.Sharpness)]
    [TestCase(OrbitraRatvarEnchantment.Tiny)]
    [TestCase(OrbitraRatvarEnchantment.SoulTap)]
    [TestCase(OrbitraRatvarEnchantment.Burn)]
    public async Task BlessingCompletesOnceAndPreservesCultEnergy(OrbitraRatvarEnchantment kind)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, weapon = default, machine = default;
        OrbitraRatvarRuleComponent cult = default!;
        var started = false;
        var duplicate = true;
        var originalDamage = 0f;
        await Server.WaitPost(() =>
        {
            (user, weapon, machine, cult) = Prepare(map.GridCoords);
            var component = SEntMan.GetComponent<OrbitraRatvarStargazerComponent>(machine);
            component.Enchantments = new() { [kind] = 1 };
            originalDamage = Server.System<SharedMeleeWeaponSystem>().GetDamage(weapon, user).GetTotal().Float();
            var system = Server.System<OrbitraRatvarStargazerSystem>();
            started = system.TryStart((machine, component), user, weapon);
            duplicate = system.TryStart((machine, component), user, weapon);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(duplicate, Is.False);
        });
        await Pair.RunSeconds(6.2f);
        await Server.WaitAssertion(() =>
        {
            var blessing = SEntMan.GetComponent<OrbitraRatvarEnchantmentComponent>(weapon);
            Assert.That(blessing.Kind, Is.EqualTo(kind));
            Assert.That(blessing.Level, Is.EqualTo(1));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarStargazerComponent>(machine).Pending, Is.Null);
            Assert.That(cult.Energy, Is.EqualTo(1000));
            Assert.That(cult.Generated, Is.Zero);
            if (kind == OrbitraRatvarEnchantment.Sharpness)
                Assert.That(Server.System<SharedMeleeWeaponSystem>().GetDamage(weapon, user).GetTotal().Float(),
                    Is.EqualTo(originalDamage + 2).Within(0.01f));
            if (kind is OrbitraRatvarEnchantment.Tiny or OrbitraRatvarEnchantment.Burn)
            {
                Assert.That(SEntMan.GetComponent<ItemComponent>(weapon).Size.Id, Is.EqualTo("Tiny"));
                Assert.That(SEntMan.GetComponent<ItemComponent>(weapon).Shape, Is.Null);
            }
            if (kind == OrbitraRatvarEnchantment.Burn)
            {
                Assert.That(blessing.FireStacks, Is.EqualTo(1));
                var damage = Server.System<SharedMeleeWeaponSystem>().GetDamage(weapon, user);
                Assert.That(damage.DamageDict.Keys, Is.EquivalentTo(new[] { "Heat" }));
                Assert.That(damage.GetTotal().Float(), Is.EqualTo(originalDamage));
            }
        });
        var cooling = true;
        var enchantedAgain = true;
        await Server.WaitPost(() =>
        {
            var component = SEntMan.GetComponent<OrbitraRatvarStargazerComponent>(machine);
            var system = Server.System<OrbitraRatvarStargazerSystem>();
            cooling = system.TryStart((machine, component), user, weapon);
            component.NextEnchant = TimeSpan.Zero;
            enchantedAgain = system.TryStart((machine, component), user, weapon);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(cooling, Is.False);
            Assert.That(enchantedAgain, Is.False);
        });
    }

    [TestCase("drop")]
    [TestCase("power")]
    [TestCase("mind")]
    [TestCase("foreign")]
    [TestCase("unanchor")]
    [TestCase("delete")]
    public async Task RitualCancelsWithoutBlessingOrCooldown(string interruption)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, weapon = default, machine = default;
        OrbitraRatvarStargazerComponent component = default!;
        var started = false;
        await Server.WaitPost(() =>
        {
            (user, weapon, machine, _) = Prepare(map.GridCoords);
            component = SEntMan.GetComponent<OrbitraRatvarStargazerComponent>(machine);
            started = Server.System<OrbitraRatvarStargazerSystem>().TryStart((machine, component), user, weapon);
        });
        await Server.WaitAssertion(() => Assert.That(started, Is.True));
        await Pair.RunSeconds(1);
        await Server.WaitPost(() =>
        {
            switch (interruption)
            {
                case "drop": Server.System<SharedHandsSystem>().TryDrop(user, weapon); break;
                case "power":
                    var rule = SEntMan.GetComponent<OrbitraRatvarStructureComponent>(machine).Rule!.Value;
                    SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Energy = 0;
                    break;
                case "mind":
                    Server.System<MindSystem>().TryGetMind(user, out var mind, out _);
                    Server.System<MindSystem>().TransferTo(mind, null);
                    break;
                case "foreign":
                    Server.System<GameTicker>().StartGameRule(CultRule, out var other);
                    SEntMan.GetComponent<OrbitraRatvarStructureComponent>(machine).Rule = other;
                    break;
                case "unanchor": Server.System<SharedTransformSystem>().Unanchor(machine); break;
                case "delete": SEntMan.DeleteEntity(machine); break;
            }
        });
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<OrbitraRatvarEnchantmentComponent>(weapon), Is.False);
            Assert.That(component.Pending, Is.Null);
            Assert.That(component.NextEnchant, Is.EqualTo(TimeSpan.Zero));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SoulTapDoesNotHealOnExamineOrMultiplyWideHits(bool actualHit)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var target = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var weapon = SEntMan.SpawnEntity("OrbitraRatvarSpear", map.GridCoords);
            var blessing = SEntMan.AddComponent<OrbitraRatvarEnchantmentComponent>(weapon);
            blessing.Kind = OrbitraRatvarEnchantment.SoulTap;
            blessing.Level = 1;
            blessing.HealingFraction = 0.1f;
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Blunt", 20);
            Server.System<DamageableSystem>().TryChangeDamage(user, damage, true);
            var melee = SEntMan.GetComponent<MeleeWeaponComponent>(weapon);
            var ev = new MeleeHitEvent([target, target], user, weapon, melee.Damage, null) { IsHit = actualHit };
            SEntMan.EventBus.RaiseLocalEvent(weapon, ev);
        });
        await Server.WaitAssertion(() =>
        {
            var spear = SProtoMan.Index(Spear);
            var melee = spear.Components["MeleeWeapon"].Component as MeleeWeaponComponent;
            var expectedHeal = MathF.Ceiling(melee!.Damage.GetTotal().Float() * 0.1f);
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(user).Float(), Is.EqualTo(20 - (actualHit ? expectedHeal : 0)));
        });
    }

    private (EntityUid, EntityUid, EntityUid, OrbitraRatvarRuleComponent) Prepare(EntityCoordinates coordinates)
    {
        Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
        var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
        cult.Energy = 1000;
        var user = SEntMan.SpawnEntity("MobHuman", coordinates);
        var minds = Server.System<MindSystem>();
        var mind = minds.CreateMind(null);
        minds.TransferTo(mind, user);
        var roles = Server.System<RoleSystem>();
        roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
        roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
        role!.Value.Comp2.Rule = rule;
        cult.Members.Add(mind);
        var sigil = SEntMan.SpawnEntity("OrbitraRatvarTransmissionSigil", coordinates);
        Server.System<OrbitraRatvarPowerSystem>().BindTransmission((sigil, SEntMan.GetComponent<OrbitraRatvarTransmissionComponent>(sigil)), rule);
        var machine = SEntMan.SpawnEntity("OrbitraRatvarStargazer", coordinates);
        SEntMan.GetComponent<OrbitraRatvarStructureComponent>(machine).Rule = rule;
        var weapon = SEntMan.SpawnEntity("OrbitraRatvarSpear", coordinates);
        Server.System<SharedHandsSystem>().TryPickup(user, weapon);
        return (user, weapon, machine, cult);
    }
}
