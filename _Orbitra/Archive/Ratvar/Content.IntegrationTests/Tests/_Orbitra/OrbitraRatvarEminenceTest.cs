using System.Linq;
using Content.Shared.Bed.Sleep;
using Content.Shared.Flash;
using Content.Shared.StatusEffectNew;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Mind;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Eye.Blinding.Components;
using Robust.Shared.Containers;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Observer authorization must not itself grant camera access, control, inventory access or membership.</summary>
[TestFixture]
public sealed partial class OrbitraRatvarEminenceTest : GameTest
{
    private static readonly EntProtoId Rule = "OrbitraRatvarRule";
    private static readonly EntProtoId Human = "MobHuman";
    private static readonly EntProtoId Role = "OrbitraMindRoleRatvar";
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };
    private EntityUid _rule;
    private EntityUid _observerMind;
    private EntityUid _targetMind;
    private EntityUid _observer;
    private EntityUid _target;
    private EntityCoordinates _origin;
    private OrbitraRatvarEminenceSystem System => Server.System<OrbitraRatvarEminenceSystem>();

    private async Task Prepare()
    {
        var map = await Pair.CreateTestMap();
        await Server.AddDummySession("RatvarObserved");
        await Server.WaitPost(() =>
        {
            _origin = map.GridCoords;
            Server.System<GameTicker>().StartGameRule(Rule, out _rule);
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationDataComponent>(station);
            Server.System<StationSystem>().AddGridToStation(station, map.Grid);
            SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Station = station;
            _observer = SEntMan.SpawnEntity(Human, _origin);
            _target = SEntMan.SpawnEntity(Human, _origin);
            var mind = Server.System<MindSystem>();
            _observerMind = mind.CreateMind(ServerSession!.UserId);
            _targetMind = mind.CreateMind(Server.PlayerMan.Sessions.Single(s => s.Name == "RatvarObserved").UserId);
            mind.TransferTo(_observerMind, _observer);
            mind.TransferTo(_targetMind, _target);
            Bind(_observerMind, _rule);
            Bind(_targetMind, _rule);
        });
        await Pair.RunTicksSync(3);
    }

    private void Bind(EntityUid mind, EntityUid rule)
    {
        var roles = Server.System<RoleSystem>();
        roles.MindAddRole(mind, Role);
        roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var membership);
        membership!.Value.Comp2.Rule = rule;
        SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Members.Add(mind);
    }

    [Test]
    public async Task ReservationAndSelectionDoNotGrantControlOrPvs()
    {
        await Prepare();
        bool reserved = false, duplicate = true, selected = false;
        await Server.WaitPost(() =>
        {
            reserved = System.TryReserve(_observerMind, _rule);
            duplicate = System.TryReserve(_targetMind, _rule);
            selected = System.TrySelect(_observerMind, _target);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(reserved, Is.True);
            Assert.That(duplicate, Is.False);
            Assert.That(selected, Is.True);
            Assert.That(ServerSession!.AttachedEntity, Is.EqualTo(_observer));
            Assert.That(ServerSession.ViewSubscriptions, Is.Empty);
            Assert.That(SEntMan.GetComponent<MindComponent>(_targetMind).OwnedEntity, Is.EqualTo(_target));
            Assert.That(SEntMan.GetComponent<MindComponent>(_observerMind).VisitingEntity, Is.Null);
            Assert.That(System.CanObserve(_observerMind, _observer), Is.False);
        });
    }

    [TestCase("death")]
    [TestCase("observer-death")]
    [TestCase("critical")]
    [TestCase("blind")]
    [TestCase("container")]
    [TestCase("map")]
    [TestCase("target-transfer")]
    [TestCase("observer-transfer")]
    [TestCase("disconnect")]
    [TestCase("observer-disconnect")]
    [TestCase("observer-purify")]
    [TestCase("purify")]
    [TestCase("round-end")]
    [TestCase("closed-eyes")]
    [TestCase("sleep")]
    [TestCase("redirect")]
    [TestCase("visibility-mask")]
    [TestCase("delete")]
    [TestCase("flash")]
    public async Task ViewIsRevoked(string reason)
    {
        _observerMind = await AcquireInvitation(await PrepareInvitation());
        await Server.WaitPost(() => _observer = SEntMan.GetComponent<MindComponent>(_observerMind).OwnedEntity!.Value);
        MapCoordinates destination = default;
        if (reason == "map")
        {
            var otherMap = await Pair.CreateTestMap();
            destination = otherMap.MapCoords;
        }
        await Server.WaitPost(() =>
        {
            System.TrySelect(_observerMind, _target);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEminenceComponent>(_observerMind).Target, Is.EqualTo(_target));
            Assert.That(ServerSession!.ViewSubscriptions, Is.EquivalentTo(new[] { _target }));
        });
        await Server.WaitPost(() =>
        {
            var minds = Server.System<MindSystem>();
            switch (reason)
            {
                case "death": Server.System<MobStateSystem>().ChangeMobState(_target, MobState.Dead); break;
                case "observer-death": Server.System<MobStateSystem>().ChangeMobState(_observer, MobState.Dead); break;
                case "critical": Server.System<MobStateSystem>().ChangeMobState(_target, MobState.Critical); break;
                case "blind": Server.System<BlindableSystem>().SetMinDamage(_target,
                    SEntMan.GetComponent<BlindableComponent>(_target).MaxDamage); break;
                case "container":
                    var box = SEntMan.SpawnEntity(null, _origin);
                    var containers = Server.System<SharedContainerSystem>();
                    containers.Insert(_target, containers.EnsureContainer<Container>(box, "test"));
                    break;
                case "map": Server.System<SharedTransformSystem>().SetMapCoordinates(_target, destination); break;
                case "target-transfer": minds.TransferTo(_targetMind, SEntMan.SpawnEntity(Human, _origin)); break;
                case "observer-transfer": minds.TransferTo(_observerMind, SEntMan.SpawnEntity(Human, _origin)); break;
                case "disconnect": minds.SetUserId(_targetMind, null); break;
                case "observer-disconnect": minds.SetUserId(_observerMind, null); break;
                case "observer-purify": Server.System<RoleSystem>().MindRemoveRole<OrbitraRatvarRoleComponent>(_observerMind); break;
                case "purify": Server.System<RoleSystem>().MindRemoveRole<OrbitraRatvarRoleComponent>(_targetMind); break;
                case "round-end": SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Lost = true; break;
                case "closed-eyes":
                    var closing = SEntMan.EnsureComponent<EyeClosingComponent>(_target);
                    Server.System<EyeClosingSystem>().SetEyelids((_target, closing), true);
                    break;
                case "sleep": SEntMan.EnsureComponent<SleepingComponent>(_target); break;
                case "redirect": Server.System<SharedEyeSystem>().SetTarget(_target, _observer); break;
                case "visibility-mask": Server.System<SharedEyeSystem>().SetVisibilityMask(_target, 3); break;
                case "delete": SEntMan.DeleteEntity(_target); break;
                case "flash": Server.System<StatusEffectsSystem>().TryAddStatusEffectDuration(_target, SharedFlashSystem.FlashedKey, TimeSpan.FromSeconds(5)); break;
            }
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            if (SEntMan.TryGetComponent<OrbitraRatvarEminenceComponent>(_observerMind, out var component))
                Assert.That(component.Target, Is.Null);
            Assert.That(ServerSession!.ViewSubscriptions, Is.Empty);
            if (SEntMan.EntityExists(_observer))
            {
                Assert.That(SEntMan.GetComponent<EyeComponent>(_observer).Target, Is.Null);
                Assert.That(SEntMan.HasComponent<OrbitraRatvarEminenceViewComponent>(_observer), Is.False);
            }
        });
    }

    [Test]
    public async Task CultsCannotShareReservationsOrTargets()
    {
        await Prepare();
        EntityUid other = default;
        bool first = false, second = false, selection = true;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(Rule, out other);
            Server.System<RoleSystem>().MindHasRole<OrbitraRatvarRoleComponent>(_targetMind, out var role);
            role!.Value.Comp2.Rule = other;
            SEntMan.GetComponent<OrbitraRatvarRuleComponent>(other).Members.Add(_targetMind);
            first = System.TryReserve(_observerMind, _rule);
            second = System.TryReserve(_targetMind, other);
            selection = System.TrySelect(_observerMind, _target);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(first && second, Is.True);
            Assert.That(selection, Is.False);
            Assert.That(System.CanReserve(_observerMind, other), Is.False);
        });
        await Server.WaitPost(() => System.Release(_targetMind));
        await Server.WaitAssertion(() =>
        {
            Assert.That(System.CanReserve(_targetMind, other), Is.True);
            Assert.That(SEntMan.HasComponent<OrbitraRatvarEminenceComponent>(_targetMind), Is.False);
            Assert.That(SEntMan.HasComponent<OrbitraRatvarEminenceComponent>(_observerMind), Is.True);
        });
    }
}
