using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Trap authority, delayed signal invalidation, target filtering and native effects.</summary>
[TestFixture]
public sealed class OrbitraRatvarTrapTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };
    private EntityUid _rule, _user, _tablet;
    private EntityCoordinates _origin;
    private OrbitraRatvarTrapSystem System => Server.System<OrbitraRatvarTrapSystem>();

    private async Task Prepare()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            _origin = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
            var grid = SEntMan.GetComponent<MapGridComponent>(map.Grid);
            var gravity = SEntMan.EnsureComponent<Content.Shared.Gravity.GravityComponent>(map.Grid);
            gravity.Enabled = gravity.Inherent = true;
            for (var x = -1; x <= 8; x++)
                for (var y = -1; y <= 2; y++)
                    Server.System<SharedMapSystem>().SetTile((map.Grid.Owner, grid), new Vector2i(x, y), map.Tile.Tile);
            Server.System<GameTicker>().StartGameRule(new EntProtoId("OrbitraRatvarRule"), out _rule);
            SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Energy = 2000;
            _user = SEntMan.SpawnEntity("MobHuman", _origin);
            var mind = Server.System<MindSystem>().CreateMind(ServerSession!.UserId);
            Server.System<MindSystem>().TransferTo(mind, _user);
            var roles = Server.System<RoleSystem>();
            roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
            roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
            role!.Value.Comp2.Rule = _rule;
            SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Members.Add(mind);
            _tablet = SEntMan.SpawnEntity("OrbitraRatvarTablet", _origin);
            Server.System<SharedHandsSystem>().TryPickupAnyHand(_user, _tablet);
            var sigil = SEntMan.SpawnEntity("OrbitraRatvarTransmissionSigil", _origin);
            Server.System<OrbitraRatvarPowerSystem>().BindTransmission(
                (sigil, SEntMan.GetComponent<OrbitraRatvarTransmissionComponent>(sigil)), _rule);
        });
    }

    private Entity<OrbitraRatvarTrapComponent> Spawn(string kind, float x = 0, float y = 0)
    {
        var uid = SEntMan.SpawnEntity("OrbitraRatvar" + kind, _origin.Offset(new Vector2(x, y)));
        SEntMan.GetComponent<OrbitraRatvarStructureComponent>(uid).Rule = _rule;
        return (uid, SEntMan.GetComponent<OrbitraRatvarTrapComponent>(uid));
    }

    [Test]
    public async Task WiringRejectsCyclesForeignOwnershipAndArmedTablet()
    {
        await Prepare();
        bool first = false, cycle = true, foreign = true, armed = true, removed = false;
        await Server.WaitPost(() =>
        {
            var firstDelay = Spawn("Delay");
            var secondDelay = Spawn("Delay", 1);
            first = System.TryLink(firstDelay, secondDelay, _user, _tablet);
            cycle = System.TryLink(secondDelay, firstDelay, _user, _tablet);
            Server.System<GameTicker>().StartGameRule(new EntProtoId("OrbitraRatvarRule"), out var foreignRule);
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(secondDelay).Rule = foreignRule;
            foreign = System.TryLink(firstDelay, secondDelay, _user, _tablet);
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(secondDelay).Rule = _rule;
            SEntMan.AddComponent<ActiveOrbitraRatvarEmpowermentComponent>(_tablet);
            armed = System.TrySelectLink(firstDelay, _user, _tablet);
            SEntMan.RemoveComponent<ActiveOrbitraRatvarEmpowermentComponent>(_tablet);
            removed = System.TryLink(firstDelay, secondDelay, _user, _tablet) && firstDelay.Comp.Outputs.Count == 0;
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(cycle, Is.False);
            Assert.That(foreign, Is.False);
            Assert.That(armed, Is.False);
            Assert.That(removed, Is.True);
        });
    }

    [TestCase("ready", true)]
    [TestCase("unanchor", false)]
    [TestCase("delete", false)]
    [TestCase("foreign", false)]
    [TestCase("empty", false)]
    public async Task DelayRevalidatesWithoutDuplicateExpenditure(string scenario, bool expected)
    {
        await Prepare();
        Entity<OrbitraRatvarTrapComponent> skewer = default;
        bool started = false, repeated = true;
        await Server.WaitPost(() =>
        {
            var lever = Spawn("Lever");
            var delay = Spawn("Delay", 1);
            skewer = Spawn("Skewer", 2);
            // Только расстановка связей: поведение привязки отдельно проверено через публичный API.
            lever.Comp.Outputs.Add(delay);
            delay.Comp.Outputs.Add(skewer);
            started = System.TryActivate(lever);
            repeated = System.TryActivate(lever);
            switch (scenario)
            {
                case "unanchor": Server.System<SharedTransformSystem>().Unanchor(delay); break;
                case "delete": SEntMan.DeleteEntity(delay); break;
                case "foreign": SEntMan.GetComponent<OrbitraRatvarStructureComponent>(delay).Rule = null; break;
                case "empty": SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Energy = 0; break;
            }
        });
        await Pair.RunSeconds(0.7f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(repeated, Is.False);
            Assert.That(skewer.Comp.Extended, Is.EqualTo(expected));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Energy,
                Is.EqualTo(scenario == "empty" ? 0 : expected ? 1999 : 2000));
        });
    }

    [Test]
    public async Task PressureFilterSpareOwnCultAndIgnoreDeadAndFlying()
    {
        await Prepare();
        bool own = true, alive = false, dead = true, flying = true;
        var context = "";
        await Server.WaitPost(() =>
        {
            var plate = Spawn("Plate");
            var victim = SEntMan.SpawnEntity("MobHuman", _origin.Offset(new Vector2(1, 0)));
            own = System.CanStep(plate, _user);
            alive = System.CanStep(plate, victim);
            var physics = SEntMan.GetComponent<PhysicsComponent>(victim);
            context = $"collision={physics.CanCollide}, status={physics.BodyStatus}, " +
                $"weightless={Server.System<Content.Shared.Gravity.SharedGravitySystem>().IsWeightless(victim)}, " +
                $"dead={Server.System<MobStateSystem>().IsDead(victim)}, " +
                $"cult={Server.System<OrbitraRatvarRuleSystem>().TryGetCult(victim, out _)}, " +
                $"plate={plate.Comp.Kind}, rule={SEntMan.GetComponent<OrbitraRatvarStructureComponent>(plate).Rule}";
            Server.System<SharedPhysicsSystem>().SetBodyStatus(victim,
                SEntMan.GetComponent<PhysicsComponent>(victim), BodyStatus.InAir);
            flying = System.CanStep(plate, victim);
            Server.System<SharedPhysicsSystem>().SetBodyStatus(victim,
                SEntMan.GetComponent<PhysicsComponent>(victim), BodyStatus.OnGround);
            Server.System<MobStateSystem>().ChangeMobState(victim, MobState.Dead);
            dead = System.CanStep(plate, victim);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(own, Is.False);
            Assert.That(alive, Is.True, context);
            Assert.That(dead, Is.False);
            Assert.That(flying, Is.False);
        });
    }

    [Test]
    public async Task SkewerDamagesHoldsAndAllowsTimedEscape()
    {
        await Prepare();
        Entity<OrbitraRatvarTrapComponent> skewer = default;
        EntityUid victim = default;
        bool escape = false;
        await Server.WaitPost(() =>
        {
            var lever = Spawn("Lever");
            skewer = Spawn("Skewer", 2);
            victim = SEntMan.SpawnEntity("MobHuman", _origin.Offset(new Vector2(2, 0)));
            lever.Comp.Outputs.Add(skewer);
            System.TryActivate(lever);
            escape = System.TryEscape(skewer, victim, victim);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That((float) Server.System<Content.Shared.Damage.Systems.DamageableSystem>().GetTotalDamage(victim), Is.GreaterThan(0f));
            Assert.That(SEntMan.GetComponent<BuckleComponent>(victim).BuckledTo, Is.EqualTo(skewer.Owner));
            Assert.That(escape, Is.True);
        });
        await Pair.RunSeconds(1f);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<BuckleComponent>(victim).Buckled, Is.True));
        await Pair.RunSeconds(4.3f);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<BuckleComponent>(victim).Buckled, Is.False));
    }

    [Test]
    public async Task FlipperUsesNativeThrowAndCooldown()
    {
        await Prepare();
        EntityUid victim = default;
        Entity<OrbitraRatvarTrapComponent> lever = default, flipper = default;
        await Server.WaitPost(() =>
        {
            lever = Spawn("Lever");
            flipper = Spawn("Flipper", 2);
            victim = SEntMan.SpawnEntity("Crowbar", _origin.Offset(new Vector2(2, 0)));
            lever.Comp.Outputs.Add(flipper);
            System.TryActivate(lever);
        });
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<Content.Shared.Throwing.ThrownItemComponent>(victim), Is.True));
        await Pair.RunSeconds(0.7f);
        await Server.WaitPost(() => System.TryActivate(lever));
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Energy, Is.EqualTo(1999));
            Assert.That(SEntMan.GetComponent<TransformComponent>(victim).Coordinates.Position,
                Is.Not.EqualTo(_origin.Offset(new Vector2(2, 0)).Position));
        });
    }

    [Test]
    public async Task PlateActivatesColocatedTrapAndDeletionReleasesVictim()
    {
        await Prepare();
        Entity<OrbitraRatvarTrapComponent> skewer = default;
        EntityUid victim = default;
        await Server.WaitPost(() =>
        {
            Spawn("Plate", 2);
            skewer = Spawn("Skewer", 2);
            victim = SEntMan.SpawnEntity("MobHuman", _origin.Offset(new Vector2(2, 0)));
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(skewer.Comp.Extended, Is.True);
            Assert.That(SEntMan.GetComponent<BuckleComponent>(victim).BuckledTo, Is.EqualTo(skewer.Owner),
                $"damage={Server.System<Content.Shared.Damage.Systems.DamageableSystem>().GetTotalDamage(victim)}, " +
                $"position={SEntMan.GetComponent<TransformComponent>(victim).Coordinates}, " +
                $"gravity={Server.System<Content.Shared.Gravity.SharedGravitySystem>().IsWeightless(victim)}");
        });
        await Server.WaitPost(() => SEntMan.DeleteEntity(skewer));
        await Pair.RunSeconds(0.2f);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<BuckleComponent>(victim).Buckled, Is.False));
    }
}
