#nullable enable

using System.Numerics;
using Content.Server.Gravity;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Orbitra.Movement;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraMobilityTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task StationaryJumpKeepsVelocityFiniteAndReturnsToGround()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            var gravity = SEntMan.EnsureComponent<GravityComponent>(map.Grid);
            Server.System<GravitySystem>().EnableGravity(map.Grid, gravity);
        });

        await Server.WaitPost(() =>
        {
            human = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(Server.PlayerMan.GetSessionById(Client.Session!.UserId), human);
        });
        await Pair.RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            var system = Server.System<OrbitraMobilitySystem>();
            var mobility = SEntMan.GetComponent<OrbitraMobilityComponent>(human);

            Assert.That(system.TryJump(new Entity<OrbitraMobilityComponent?>(human, mobility)), Is.True);

            var maneuver = SEntMan.GetComponent<OrbitraActiveManeuverComponent>(human);
            Assert.That(maneuver.Direction, Is.EqualTo(Vector2.Zero),
                "Прыжок на месте не должен нормализовать нулевой вектор в NaN.");
            AssertFiniteVelocity();
        });

        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<OrbitraActiveManeuverComponent>(human), Is.False);
            Assert.That(SEntMan.GetComponent<PhysicsComponent>(human).BodyStatus, Is.EqualTo(BodyStatus.OnGround));
            AssertFiniteVelocity();
        });

        void AssertFiniteVelocity()
        {
            var velocity = SEntMan.GetComponent<PhysicsComponent>(human).LinearVelocity;
            Assert.That(float.IsFinite(velocity.X) && float.IsFinite(velocity.Y), Is.True,
                "Манёвр не должен записывать NaN или бесконечность в скорость тела.");
        }
    }

    [Test]
    public async Task KnockdownInterruptsRollAndStopsVelocity()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            var gravity = SEntMan.EnsureComponent<GravityComponent>(map.Grid);
            Server.System<GravitySystem>().EnableGravity(map.Grid, gravity);
        });

        await Server.WaitPost(() =>
        {
            human = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(Server.PlayerMan.GetSessionById(Client.Session!.UserId), human);
        });
        await Pair.RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            var system = Server.System<OrbitraMobilitySystem>();
            var mobility = SEntMan.GetComponent<OrbitraMobilityComponent>(human);

            Assert.That(system.TryRoll(new Entity<OrbitraMobilityComponent?>(human, mobility), Vector2.UnitX), Is.True);
            Assert.That(SEntMan.HasComponent<OrbitraActiveManeuverComponent>(human), Is.True);
            Assert.That(SEntMan.HasComponent<OrbitraProneComponent>(human), Is.True);
        });

        await Pair.RunTicksSync(1);
        await Server.WaitAssertion(() =>
        {
            var velocity = SEntMan.GetComponent<PhysicsComponent>(human).LinearVelocity;
            Assert.That(velocity.X, Is.GreaterThan(0f));
        });

        await Server.WaitAssertion(() =>
        {
            var crawler = SEntMan.GetComponent<CrawlerComponent>(human);
            Assert.That(Server.System<SharedStunSystem>().TryKnockdown(
                new Entity<CrawlerComponent?>(human, crawler), TimeSpan.FromSeconds(1), force: true), Is.True);
            Assert.That(SEntMan.HasComponent<OrbitraActiveManeuverComponent>(human), Is.False);
            Assert.That(SEntMan.HasComponent<OrbitraProneComponent>(human), Is.False);
            Assert.That(Server.System<StandingStateSystem>().IsDown(human), Is.True);
            Assert.That(SEntMan.GetComponent<PhysicsComponent>(human).LinearVelocity, Is.EqualTo(Vector2.Zero));
        });
    }

    [Test]
    public async Task StandingUpRestoresSpeedWithoutJump()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;
        float standingWalkModifier = 1f;
        float standingSprintModifier = 1f;

        await Server.WaitPost(() =>
        {
            var gravity = SEntMan.EnsureComponent<GravityComponent>(map.Grid);
            Server.System<GravitySystem>().EnableGravity(map.Grid, gravity);
        });

        await Server.WaitPost(() =>
        {
            human = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(Server.PlayerMan.GetSessionById(Client.Session!.UserId), human);

            var mobility = SEntMan.GetComponent<OrbitraMobilityComponent>(human);
            mobility.StandDuration = 0.05f;
            mobility.RollDuration = 0.05f;
            SEntMan.Dirty(human, mobility);

            var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(human);
            standingWalkModifier = speed.WalkSpeedModifier;
            standingSprintModifier = speed.SprintSpeedModifier;
        });
        await Pair.RunTicksSync(20);

        await Server.WaitPost(() =>
        {
            var system = Server.System<OrbitraMobilitySystem>();
            var mobility = SEntMan.GetComponent<OrbitraMobilityComponent>(human);
            var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(human);

            Assert.That(system.TryRoll(new Entity<OrbitraMobilityComponent?>(human, mobility), Vector2.UnitX), Is.True);
            Assert.That(speed.WalkSpeedModifier,
                Is.EqualTo(standingWalkModifier * mobility.CrawlSpeedModifier).Within(0.001f));
            Assert.That(speed.SprintSpeedModifier,
                Is.EqualTo(standingSprintModifier * mobility.CrawlSpeedModifier).Within(0.001f));
        });

        await Pair.RunTicksSync(5);
        await Server.WaitPost(() =>
        {
            var system = Server.System<OrbitraMobilitySystem>();
            var mobility = SEntMan.GetComponent<OrbitraMobilityComponent>(human);
            Assert.That(system.TryStand(new Entity<OrbitraMobilityComponent?>(human, mobility)), Is.True);
        });

        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<OrbitraProneComponent>(human), Is.False);
            var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(human);
            Assert.That(speed.WalkSpeedModifier, Is.EqualTo(standingWalkModifier).Within(0.001f));
            Assert.That(speed.SprintSpeedModifier, Is.EqualTo(standingSprintModifier).Within(0.001f));
        });
    }
}
