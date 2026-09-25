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
    [TestCase("caster-move", false)]
    [TestCase("sigil-delete", false)]
    [TestCase("tablet-drop", false)]
    [TestCase("hand-change", false)]
    [TestCase("target-mind", false)]
    [TestCase("caster-mind", false)]
    [TestCase("caster-damage", false)]
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
        var started = false;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
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
            targetMind = minds.CreateMind(null);
            minds.TransferTo(targetMind, target);
            var uid = SEntMan.SpawnEntity("OrbitraRatvarTablet", center);
            tablet = (uid, SEntMan.GetComponent<OrbitraRatvarTabletComponent>(uid));
            pickedUp = Server.System<SharedHandsSystem>().TryPickup(user, uid);
            started = Server.System<OrbitraRatvarRuleSystem>().TryStartConversion(tablet, user, target);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(pickedUp, Is.True);
            Assert.That(started, Is.True, "Every-tick validation must also permit the initial DoAfter check.");
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
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<RoleSystem>().MindHasRole<OrbitraRatvarRoleComponent>(targetMind), Is.EqualTo(expected));
            Assert.That(tablet.Comp.Busy, Is.False);
            Assert.That(tablet.Comp.RitualMind, Is.Null);
            Assert.That(tablet.Comp.TargetMind, Is.Null);
        });
    }
}
