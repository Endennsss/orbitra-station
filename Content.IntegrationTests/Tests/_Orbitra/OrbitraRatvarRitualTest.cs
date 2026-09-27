using Robust.Shared.Prototypes;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Mind;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Cuffs.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Mindshield;
using Content.Shared.Mindshield.Components;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Exercises real timed conversion rather than granting the role directly.</summary>
[TestFixture]
public sealed class OrbitraRatvarRitualTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [TestCase("inside", true)]
    [TestCase("leave-return", false)]
    [TestCase("caster-move", true)]
    [TestCase("sigil-delete", false)]
    [TestCase("tablet-drop", true)]
    [TestCase("hand-change", true)]
    [TestCase("target-mind", false)]
    [TestCase("caster-mind", false)]
    [TestCase("caster-damage", true)]
    [TestCase("helper-leave", false)]
    [TestCase("no-tablet", true)]
    [TestCase("vanguard", true)]
    [TestCase("kindle", true)]
    [TestCase("manacles", true)]
    [TestCase("foreign-sigil", false)]
    [TestCase("unanchor", false)]
    [TestCase("mindshield", false)]
    [TestCase("duplicate-sigil", true)]
    public async Task ConversionChecksEntireFiveSecondRitual(string interruption, bool expected)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid target = default;
        EntityUid targetMind = default;
        EntityUid sigil = default;
        EntityCoordinates center = default;
        Entity<OrbitraRatvarTabletComponent> tablet = default;
        var pickedUp = false;
        var prepared = false;
        OrbitraRatvarRuleComponent cult = default!;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            sigil = SEntMan.SpawnEntity("OrbitraRatvarConversionSigil", map.GridCoords);
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(sigil).Rule = rule;
            center = SEntMan.GetComponent<TransformComponent>(sigil).Coordinates;
            user = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(center.EntityId, center.Position + new Vector2(-1, 0)));
            target = SEntMan.SpawnEntity("MobHuman", center);
            var minds = Server.System<MindSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, user);
            var roles = Server.System<RoleSystem>();
            roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
            roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
            role!.Value.Comp2.Rule = rule;
            cult.Members.Add(mind);
            cult.Energy = 1000;
            cult.Generated = cult.TierTwoEnergy;
            for (var i = 0; i < cult.TierTwoConverts; i++)
                cult.Converted.Add(SEntMan.SpawnEntity(null, MapCoordinates.Nullspace));
            targetMind = minds.CreateMind(null);
            minds.TransferTo(targetMind, target);
            var uid = SEntMan.SpawnEntity("OrbitraRatvarTablet", center);
            tablet = (uid, SEntMan.GetComponent<OrbitraRatvarTabletComponent>(uid));
            pickedUp = Server.System<SharedHandsSystem>().TryPickup(user, uid);
            if (interruption == "no-tablet") SEntMan.DeleteEntity(uid);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(pickedUp, Is.True);
        });
        await Pair.RunSeconds(1);
        await Server.WaitPost(() =>
        {
            var transforms = Server.System<SharedTransformSystem>();
            switch (interruption)
            {
                case "inside":
                    transforms.SetCoordinates(target, new EntityCoordinates(center.EntityId, center.Position + new Vector2(0.4f, 0)));
                    break;
                case "leave-return":
                    // Цель покидает печать, но остаётся в радиусе взаимодействия с проводящим ритуал.
                    transforms.SetCoordinates(target, new EntityCoordinates(center.EntityId, center.Position + new Vector2(0, 0.8f)));
                    break;
                case "caster-move":
                    transforms.SetCoordinates(user, new EntityCoordinates(center.EntityId, center.Position + new Vector2(-1, 0.4f)));
                    break;
                case "helper-leave":
                    transforms.SetCoordinates(user, center.Offset(new Vector2(-4, 0)));
                    break;
                case "foreign-sigil":
                    Server.System<GameTicker>().StartGameRule(CultRule, out var otherRule);
                    SEntMan.GetComponent<OrbitraRatvarStructureComponent>(sigil).Rule = otherRule;
                    break;
                case "unanchor":
                    transforms.Unanchor(sigil);
                    break;
                case "mindshield":
                    SEntMan.EnsureComponent<MindShieldComponent>(target);
                    Server.System<MindShieldSystem>().RefreshMindshieldStatus(target);
                    break;
                case "duplicate-sigil":
                    var second = SEntMan.SpawnEntity("OrbitraRatvarConversionSigil", center);
                    SEntMan.GetComponent<OrbitraRatvarStructureComponent>(second).Rule =
                        SEntMan.GetComponent<OrbitraRatvarStructureComponent>(sigil).Rule;
                    break;
                case "vanguard":
                    prepared = Server.System<OrbitraRatvarRuleSystem>().TryCompleteScripture(tablet, user, "OrbitraRatvarVanguard");
                    break;
                case "kindle":
                case "manacles":
                    prepared = Server.System<OrbitraRatvarRuleSystem>().TryCompleteScripture(tablet, user,
                        interruption == "kindle" ? "OrbitraRatvarKindle" : "OrbitraRatvarManacles");
                    Server.System<SharedInteractionSystem>().InteractDoAfter(user, tablet, target, center, true);
                    break;
                case "sigil-delete":
                    SEntMan.DeleteEntity(sigil);
                    break;
                case "tablet-drop":
                    Server.System<SharedHandsSystem>().TryDrop(user, tablet.Owner);
                    break;
                case "hand-change":
                    var hands = SEntMan.GetComponent<HandsComponent>(user);
                    Server.System<SharedHandsSystem>().TrySetActiveHand(user,
                        hands.Hands.Keys.First(hand => hand != hands.ActiveHandId));
                    break;
                case "target-mind":
                    Server.System<MindSystem>().TransferTo(targetMind, null);
                    break;
                case "caster-mind":
                    var minds = Server.System<MindSystem>();
                    minds.TryGetMind(user, out var caster, out _);
                    minds.TransferTo(caster, null);
                    break;
                case "caster-damage":
                    var damage = new DamageSpecifier();
                    damage.DamageDict.Add("Blunt", 5);
                    Server.System<DamageableSystem>().TryChangeDamage(user, damage, true);
                    break;
            }
        });
        await Pair.RunTicksSync(2);
        if (interruption == "leave-return")
            await Server.WaitPost(() => Server.System<SharedTransformSystem>().SetCoordinates(target, center));
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() => Assert.That(Server.System<RoleSystem>().MindIsAntagonist(targetMind), Is.False));
        await Pair.RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<RoleSystem>().MindHasRole<OrbitraRatvarRoleComponent>(targetMind), Is.EqualTo(expected));
            Assert.That(cult.Converted.Count, Is.EqualTo(cult.TierTwoConverts + (expected ? 1 : 0)));
            if (interruption is "kindle" or "manacles" or "vanguard") Assert.That(prepared, Is.True);
            if (interruption == "kindle")
                Assert.That(Server.System<StatusEffectsSystem>().TryGetStatusEffect(target, SharedStunSystem.StunId, out _), Is.True);
            if (interruption == "manacles") Assert.That(SEntMan.GetComponent<CuffableComponent>(target).CuffedHandCount, Is.EqualTo(2));
            if (interruption == "vanguard") Assert.That(SEntMan.HasComponent<ActiveOrbitraRatvarVanguardComponent>(user), Is.True);
            if (interruption != "no-tablet")
            {
                Assert.That(tablet.Comp.Busy, Is.False);
                Assert.That(tablet.Comp.RitualMind, Is.Null);
            }
        });
    }
}
