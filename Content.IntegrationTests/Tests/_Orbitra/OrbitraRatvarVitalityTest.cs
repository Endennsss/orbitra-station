using System;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Traits.Assorted;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Vitality isolation, continuous presence, actual healing costs and owned-body revival.</summary>
[TestFixture]
public sealed class OrbitraRatvarVitalityTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };
    private Entity<OrbitraRatvarVitalityComponent> _sigil;
    private Entity<OrbitraRatvarRuleComponent> _cult;
    private EntityUid _body;
    private EntityCoordinates _origin;
    private OrbitraRatvarVitalitySystem System => Server.System<OrbitraRatvarVitalitySystem>();

    private async Task Prepare(bool member = false)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            _origin = map.GridCoords;
            Server.System<GameTicker>().StartGameRule("OrbitraRatvarRule", out var rule);
            _cult = (rule, SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule));
            var sigil = SEntMan.SpawnEntity("OrbitraRatvarVitality", _origin);
            _sigil = (sigil, SEntMan.GetComponent<OrbitraRatvarVitalityComponent>(sigil));
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(sigil).Rule = rule;
            _body = SEntMan.SpawnEntity("MobHuman", _origin);
            if (!member) return;
            var mind = Server.System<MindSystem>().CreateMind(null);
            Server.System<MindSystem>().TransferTo(mind, _body);
            var roles = Server.System<RoleSystem>();
            roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
            roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
            role!.Value.Comp2.Rule = rule;
            _cult.Comp.Members.Add(mind);
        });
    }

    [Test]
    public async Task DrainDoesNotDuplicateOrShareResource()
    {
        await Prepare();
        var first = false;
        var duplicate = false;
        var energy = 0;
        EntityUid otherRule = default;
        await Server.WaitPost(() =>
        {
            energy = _cult.Comp.Energy;
            Server.System<GameTicker>().StartGameRule("OrbitraRatvarRule", out otherRule);
            System.TryBegin(_sigil, _body);
            _sigil.Comp.FinishAt = Server.Resolve<IGameTiming>().CurTime;
            first = System.TryPulse(_sigil);
            _sigil.Comp.FinishAt = Server.Resolve<IGameTiming>().CurTime;
            duplicate = System.TryPulse(_sigil);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(duplicate, Is.False);
            Assert.That(_cult.Comp.Vitality.Float(), Is.EqualTo(10));
            Assert.That(_cult.Comp.Energy, Is.EqualTo(energy));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(otherRule).Vitality.Float(), Is.Zero);
        });
    }

    [Test]
    public async Task LeavingCancelsChargeAndConvertibleMindIsProtected()
    {
        await Prepare();
        await Server.WaitPost(() =>
        {
            System.TryBegin(_sigil, _body);
            Server.System<SharedTransformSystem>().SetCoordinates(_body, _origin.Offset(new Vector2(3, 0)));
        });
        await Pair.RunSeconds(2.2f);
        await Server.WaitAssertion(() => Assert.That(_cult.Comp.Vitality.Float(), Is.Zero));
        await Server.WaitPost(() =>
        {
            Server.System<SharedTransformSystem>().SetCoordinates(_body, _origin);
            var mind = Server.System<MindSystem>().CreateMind(null);
            Server.System<MindSystem>().TransferTo(mind, _body);
        });
        await Server.WaitAssertion(() => Assert.That(System.CanAffect(_sigil, _body, out _), Is.False));
        await Server.WaitPost(() => _cult.Comp.SummonAt = Server.Resolve<IGameTiming>().CurTime + TimeSpan.FromMinutes(5));
        await Server.WaitAssertion(() => Assert.That(System.CanAffect(_sigil, _body, out _), Is.True));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task HealingAndRevivalSpendOnlyOwnVitality(bool dead)
    {
        await Prepare(true);
        var success = false;
        await Server.WaitPost(() =>
        {
            var damage = new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>("Blunt"), dead ? 220 : 10);
            Server.System<DamageableSystem>().TryChangeDamage(_body, damage);
            if (dead) Server.System<MobStateSystem>().ChangeMobState(_body, MobState.Dead);
            _cult.Comp.Vitality = 500;
            System.TryBegin(_sigil, _body);
            _sigil.Comp.FinishAt = Server.Resolve<IGameTiming>().CurTime;
            success = System.TryPulse(_sigil);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(_cult.Comp.Vitality.Float(), Is.EqualTo(dead ? 348f : 498.5f).Within(0.01));
            Assert.That(SEntMan.GetComponent<MobStateComponent>(_body).CurrentState, Is.Not.EqualTo(MobState.Dead));
            var injuries = Server.System<DamageableSystem>().GetPositiveDamage((_body, SEntMan.GetComponent<DamageableComponent>(_body)));
            Assert.That(injuries.GetTotal().Float(), Is.EqualTo(dead ? 0 : 5).Within(0.01));
        });
    }

    [Test]
    public async Task UnrevivableBodyAndEmptyPoolAreRejected()
    {
        await Prepare(true);
        var success = true;
        await Server.WaitPost(() =>
        {
            Server.System<MobStateSystem>().ChangeMobState(_body, MobState.Dead);
            SEntMan.EnsureComponent<UnrevivableComponent>(_body);
        });
        await Server.WaitAssertion(() => Assert.That(System.CanAffect(_sigil, _body, out _), Is.False));
        await Server.WaitPost(() =>
        {
            SEntMan.RemoveComponent<UnrevivableComponent>(_body);
            System.TryBegin(_sigil, _body);
            _sigil.Comp.FinishAt = Server.Resolve<IGameTiming>().CurTime;
            success = System.TryPulse(_sigil);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(_cult.Comp.Vitality.Float(), Is.Zero);
        });
    }
}
