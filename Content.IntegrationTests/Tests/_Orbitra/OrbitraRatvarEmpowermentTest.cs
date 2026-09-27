using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mindshield;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraRatvarEmpowermentTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";
    private static readonly EntProtoId Muted = "StatusEffectMuted";
    private EntityUid _rule, _user, _mind, _target;
    private Entity<OrbitraRatvarTabletComponent> _tablet;
    private OrbitraRatvarRuleComponent _cult = default!;

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    private async Task Arrange()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(CultRule, out _rule);
            _cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule);
            _cult.Energy = 1000;
            _cult.Generated = _cult.TierThreeEnergy;
            for (var i = 0; i < _cult.TierThreeConverts; i++)
                _cult.Converted.Add(SEntMan.SpawnEntity(null, MapCoordinates.Nullspace));
            _user = SEntMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(0.5f)));
            _mind = AddMember(_user, _rule);
            _target = SEntMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(1.5f, 0.5f)));
            var tablet = SEntMan.SpawnEntity("OrbitraRatvarTablet", map.GridCoords);
            Server.System<SharedHandsSystem>().TryPickup(_user, tablet);
            _tablet = (tablet, SEntMan.GetComponent<OrbitraRatvarTabletComponent>(tablet));
        });
    }

    [TestCase("normal", true)]
    [TestCase("shield", true)]
    [TestCase("own-cult", false)]
    [TestCase("foreign-cult", false)]
    [TestCase("range", false)]
    [TestCase("wall", false)]
    [TestCase("empty", false)]
    [TestCase("dropped", false)]
    [TestCase("mind", false)]
    [TestCase("expired", false)]
    public async Task KindleRevalidatesPreparedSpell(string scenario, bool expected)
    {
        await Arrange();
        var prepared = false;
        await Server.WaitPost(() => prepared = Server.System<OrbitraRatvarRuleSystem>()
            .TryStartScripture(_tablet, _user, "OrbitraRatvarKindle"));
        Assert.That(prepared, Is.True);
        await Pair.RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<ActiveOrbitraRatvarEmpowermentComponent>(_tablet), Is.True);
            Assert.That(_cult.Energy, Is.EqualTo(1000), "Preparation must not spend energy.");
        });
        var applied = false;
        var repeated = false;
        await Server.WaitPost(() =>
        {
            switch (scenario)
            {
                case "shield":
                    SEntMan.EnsureComponent<MindShieldComponent>(_target);
                    Server.System<MindShieldSystem>().RefreshMindshieldStatus(_target);
                    break;
                case "own-cult": AddMember(_target, _rule); break;
                case "foreign-cult":
                    Server.System<GameTicker>().StartGameRule(CultRule, out var other);
                    AddMember(_target, other);
                    break;
                case "range": Server.System<SharedTransformSystem>().SetCoordinates(_target,
                    SEntMan.GetComponent<TransformComponent>(_user).Coordinates.Offset(new Vector2(8, 0))); break;
                case "wall": SEntMan.SpawnEntity("WallSolid", SEntMan.GetComponent<TransformComponent>(_target).Coordinates); break;
                case "empty": _cult.Energy = 124; break;
                case "dropped": Server.System<SharedHandsSystem>().TryDrop(_user, _tablet); break;
                case "mind": Server.System<MindSystem>().TransferTo(_mind, _target); break;
                case "expired": SEntMan.GetComponent<ActiveOrbitraRatvarEmpowermentComponent>(_tablet).Expires = TimeSpan.Zero; break;
            }
            applied = Server.System<OrbitraRatvarRuleSystem>().TryTargetEmpowerment(_tablet, _user, _target);
            repeated = Server.System<OrbitraRatvarRuleSystem>().TryTargetEmpowerment(_tablet, _user, _target);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(applied, Is.EqualTo(expected));
            Assert.That(repeated, Is.False);
            Assert.That(_cult.Energy, Is.EqualTo(scenario == "empty" ? 124 : expected ? 875 : 1000));
            var status = Server.System<StatusEffectsSystem>();
            Assert.That(status.TryGetStatusEffect(_target, Muted, out _), Is.EqualTo(expected));
            Assert.That(status.TryGetStatusEffect(_target, SharedStunSystem.StunId, out _), Is.EqualTo(scenario == "normal"));
        });
    }

    [Test]
    public async Task PreparationLosesAuthorityWhenMindChanges()
    {
        await Arrange();
        await Server.WaitAssertion(() => Assert.That(Server.System<OrbitraRatvarRuleSystem>()
            .TryStartScripture(_tablet, _user, "OrbitraRatvarKindle"), Is.True));
        await Pair.RunSeconds(1);
        await Server.WaitPost(() => Server.System<MindSystem>().TransferTo(_mind, _target));
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_tablet.Comp.Busy, Is.False);
            Assert.That(SEntMan.HasComponent<ActiveOrbitraRatvarEmpowermentComponent>(_tablet), Is.False);
            Assert.That(_cult.Energy, Is.EqualTo(1000));
        });
    }

    [TestCase("own", true)]
    [TestCase("foreign", false)]
    [TestCase("healthy", false)]
    [TestCase("godmode", false)]
    public async Task CompromiseHealsOnlyOwnCultAndChargesActualBacklash(string scenario, bool expected)
    {
        await Arrange();
        var applied = false;
        await Server.WaitPost(() =>
        {
            var owner = _rule;
            if (scenario == "foreign") Server.System<GameTicker>().StartGameRule(CultRule, out owner);
            AddMember(_target, owner);
            if (scenario == "godmode") SEntMan.AddComponent<GodmodeComponent>(_user);
            if (scenario != "healthy")
            {
                var damage = new DamageSpecifier();
                damage.DamageDict["Blunt"] = 40;
                damage.DamageDict["Heat"] = 20;
                damage.DamageDict["Poison"] = 10;
                Server.System<DamageableSystem>().TryChangeDamage((_target, null), damage, ignoreResistances: true);
            }
            var system = Server.System<OrbitraRatvarRuleSystem>();
            system.TryCompleteScripture(_tablet, _user, "OrbitraRatvarCompromise");
            applied = system.TryTargetEmpowerment(_tablet, _user, _target);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(applied, Is.EqualTo(expected));
            Assert.That(_cult.Energy, Is.EqualTo(expected ? 920 : 1000));
            if (!expected) return;
            var target = Server.System<DamageableSystem>().GetAllDamage((_target, null)).DamageDict;
            Assert.That(target["Blunt"].Float(), Is.EqualTo(16).Within(0.01));
            Assert.That(target["Heat"].Float(), Is.EqualTo(8).Within(0.01));
            Assert.That(target["Poison"].Float(), Is.EqualTo(10).Within(0.01));
            Assert.That(Server.System<DamageableSystem>().GetAllDamage((_user, null)).DamageDict["Poison"].Float(), Is.EqualTo(18).Within(0.01));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ManaclesRequireUninterruptedTargetAction(bool moveTarget)
    {
        await Arrange();
        var started = false;
        await Server.WaitPost(() =>
        {
            var system = Server.System<OrbitraRatvarRuleSystem>();
            system.TryCompleteScripture(_tablet, _user, "OrbitraRatvarManacles");
            started = system.TryTargetEmpowerment(_tablet, _user, _target);
        });
        Assert.That(started, Is.True);
        await Pair.RunSeconds(1);
        if (moveTarget)
            await Server.WaitPost(() => Server.System<SharedTransformSystem>().SetCoordinates(_target,
                SEntMan.GetComponent<TransformComponent>(_user).Coordinates.Offset(new Vector2(3, 0))));
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CuffableComponent>(_target).CuffedHandCount, Is.EqualTo(moveTarget ? 0 : 2));
            Assert.That(_cult.Energy, Is.EqualTo(moveTarget ? 1000 : 975));
            Assert.That(_tablet.Comp.Busy, Is.False);
        });
    }

    [TestCase("expire")]
    [TestCase("mind")]
    [TestCase("purify")]
    public async Task VanguardBlocksControlButEndsWithItsAuthority(string ending)
    {
        await Arrange();
        var invoked = false;
        var recited = false;
        var stun = false;
        var stamina = new BeforeStaminaDamageEvent(100);
        await Server.WaitPost(() =>
        {
            var system = Server.System<OrbitraRatvarRuleSystem>();
            invoked = system.TryCompleteScripture(_tablet, _user, "OrbitraRatvarVanguard");
            recited = system.TryStartScripture(_tablet, _user, "OrbitraRatvarKindle");
            stun = Server.System<SharedStunSystem>().TryUpdateParalyzeDuration(_user, TimeSpan.FromSeconds(1));
            SEntMan.EventBus.RaiseLocalEvent(_user, ref stamina);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(invoked, Is.True);
            Assert.That(recited, Is.False);
            Assert.That(stun, Is.False);
            Assert.That(stamina.Cancelled, Is.True);
            Assert.That(_cult.Energy, Is.EqualTo(850));
        });
        await Server.WaitPost(() =>
        {
            if (ending == "expire") SEntMan.GetComponent<ActiveOrbitraRatvarVanguardComponent>(_user).Expires = TimeSpan.Zero;
            if (ending == "mind") Server.System<MindSystem>().TransferTo(_mind, _target);
            if (ending == "purify") Server.System<OrbitraRatvarRuleSystem>().TryPurify((_rule, _cult), _mind);
            stamina = new BeforeStaminaDamageEvent(100);
            SEntMan.EventBus.RaiseLocalEvent(_user, ref stamina);
        });
        Assert.That(stamina.Cancelled, Is.False, "Revocation must take effect before the cleanup tick.");
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<ActiveOrbitraRatvarVanguardComponent>(_user), Is.False));
    }

    [Test]
    public async Task VanguardAllowsStaminaRecovery()
    {
        await Arrange();
        var invoked = false;
        var recovery = new BeforeStaminaDamageEvent(-100);
        await Server.WaitPost(() =>
        {
            invoked = Server.System<OrbitraRatvarRuleSystem>()
                .TryCompleteScripture(_tablet, _user, "OrbitraRatvarVanguard");
            SEntMan.EventBus.RaiseLocalEvent(_user, ref recovery);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(invoked, Is.True);
            Assert.That(recovery.Cancelled, Is.False, "Protection must not cancel stamina recovery.");
        });
    }

    private EntityUid AddMember(EntityUid body, EntityUid rule)
    {
        var minds = Server.System<MindSystem>();
        var mind = minds.CreateMind(null);
        minds.TransferTo(mind, body);
        var roles = Server.System<RoleSystem>();
        roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
        roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
        role!.Value.Comp2.Rule = rule;
        SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Members.Add(mind);
        return mind;
    }
}
