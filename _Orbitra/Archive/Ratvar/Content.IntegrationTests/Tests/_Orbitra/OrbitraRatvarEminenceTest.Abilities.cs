using System.Linq;
using System.Numerics;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Components;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed partial class OrbitraRatvarEminenceTest
{
    private static readonly EntProtoId Spire = "OrbitraRatvarEminenceSpire";
    private static readonly EntProtoId Waygate = "OrbitraRatvarTravelPoint";
    private static readonly EntProtoId Transmission = "OrbitraRatvarTransmissionSigil";
    private static readonly EntProtoId Ark = "OrbitraRatvarArk";

    private EntityUid SpawnBound(EntProtoId prototype, Vector2 offset)
    {
        var map = Server.System<SharedMapSystem>();
        var grid = SEntMan.GetComponent<Robust.Shared.Map.Components.MapGridComponent>(_origin.EntityId);
        var floor = map.GetTileRef(_origin.EntityId, grid, _origin).Tile;
        for (var x = -2; x <= 2; x++)
        for (var y = -2; y <= 2; y++)
            map.SetTile(_origin.EntityId, grid, _origin.Offset(new Vector2(x, y)), floor);
        var entity = SEntMan.SpawnEntity(prototype, _origin.Offset(offset));
        SEntMan.GetComponent<OrbitraRatvarStructureComponent>(entity).Rule = _rule;
        return entity;
    }

    private async Task<(EntityUid Avatar, EntityUid Mind, EntityUid Destination)> PrepareAbilities()
    {
        var mind = await AcquireInvitation(await PrepareInvitation());
        EntityUid avatar = default, destination = default;
        await Server.WaitPost(() =>
        {
            avatar = SEntMan.GetComponent<MindComponent>(mind).OwnedEntity!.Value;
            SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Energy = 1000;
            var sigil = SpawnBound(Transmission, new Vector2(0, 1));
            Server.System<OrbitraRatvarPowerSystem>().BindTransmission(
                (sigil, SEntMan.GetComponent<OrbitraRatvarTransmissionComponent>(sigil)), _rule);
            destination = SpawnBound(Waygate, new Vector2(1, 0));
            System.TrySelect(mind, _target);
        });
        await Pair.RunTicksSync(3);
        return (avatar, mind, destination);
    }

    [Test]
    public async Task SummoningNormalBeaconSupportsObjectionAndNativeAcquisition()
    {
        await Prepare();
        EntityUid spire = default, invitation = default;
        bool started = false, cancelled = false, duplicate = true;
        await Server.WaitPost(() =>
        {
            spire = SpawnBound(Spire, new Vector2(0, 1));
            var beacon = SEntMan.GetComponent<OrbitraRatvarEminenceSpireComponent>(spire);
            beacon.ObjectionPeriod = TimeSpan.FromSeconds(0.2);
            started = System.TrySummon((spire, beacon), _target);
            duplicate = System.TryReserve(_observerMind, _rule);
            cancelled = System.TrySummon((spire, beacon), _target);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(started && cancelled, Is.True);
            Assert.That(duplicate, Is.False);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEminenceSpireComponent>(spire).SummonAt, Is.Null);
        });
        await Server.WaitPost(() => System.TrySummon((spire, SEntMan.GetComponent<OrbitraRatvarEminenceSpireComponent>(spire)), _target));
        await Pair.RunSeconds(0.5f);
        await Server.WaitPost(() =>
        {
            var invitations = SEntMan.EntityQueryEnumerator<OrbitraRatvarEminenceInvitationComponent>();
            invitations.MoveNext(out invitation, out _);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).TestTier, Is.Null);
            Assert.That(SEntMan.HasComponent<GhostRoleComponent>(invitation), Is.True);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEminenceInvitationComponent>(invitation).TestOnly, Is.False);
        });
        var acquired = await AcquireInvitation(invitation);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<OrbitraRatvarEminenceComponent>(acquired).Rule, Is.EqualTo(_rule)));
    }

    [TestCase("unanchor")]
    [TestCase("destroy")]
    [TestCase("end")]
    public async Task SummoningInvalidatedBeaconNeverOffersRole(string invalidation)
    {
        await Prepare();
        var started = false;
        await Server.WaitPost(() =>
        {
            var spire = SpawnBound(Spire, new Vector2(0, 1));
            var beacon = SEntMan.GetComponent<OrbitraRatvarEminenceSpireComponent>(spire);
            beacon.ObjectionPeriod = TimeSpan.FromSeconds(0.2);
            started = System.TrySummon((spire, beacon), _target);
            switch (invalidation)
            {
                case "unanchor": Server.System<SharedTransformSystem>().Unanchor(spire); break;
                case "destroy": SEntMan.QueueDeleteEntity(spire); break;
                case "end": SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Lost = true; break;
            }
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(SEntMan.EntityQuery<OrbitraRatvarEminenceInvitationComponent>(), Is.Empty);
        });
    }

    [Test]
    public async Task RecallNetworkRequestSpendsOnceAndPreservesControl()
    {
        var (avatar, mind, destination) = await PrepareAbilities();
        NetEntity netAvatar = default, netDestination = default;
        await Server.WaitPost(() =>
        {
            SEntMan.GetComponent<OrbitraRatvarEminenceAvatarComponent>(avatar).RecallDelay = TimeSpan.FromSeconds(0.5);
            netAvatar = SEntMan.GetNetEntity(avatar);
            netDestination = SEntMan.GetNetEntity(destination);
        });
        await Pair.RunTicksSync(15);
        await Client.WaitPost(() =>
        {
            Client.System<Robust.Client.GameObjects.UserInterfaceSystem>().TryGetOpenUi(
                CEntMan.GetEntity(netAvatar), OrbitraRatvarEminenceUiKey.Key, out var ui);
            ui!.SendMessage(new OrbitraRatvarEminenceRecallMessage(netDestination));
            ui.SendMessage(new OrbitraRatvarEminenceRecallMessage(netDestination));
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<TransformComponent>(_target).Coordinates,
                Is.EqualTo(SEntMan.GetComponent<TransformComponent>(destination).Coordinates));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Energy, Is.EqualTo(900));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEminenceComponent>(mind).Recalling, Is.False);
            Assert.That(ServerSession!.AttachedEntity, Is.EqualTo(avatar));
            Assert.That(SEntMan.GetComponent<MindComponent>(_targetMind).OwnedEntity, Is.EqualTo(_target));
            Assert.That(System.CanRecall((avatar, SEntMan.GetComponent<OrbitraRatvarEminenceAvatarComponent>(avatar)), destination, out _, out _, out _), Is.False);
        });
    }

    [TestCase("move")]
    [TestCase("power")]
    [TestCase("clear")]
    [TestCase("foreign")]
    public async Task RecallInvalidationDoesNotMoveOrSpend(string invalidation)
    {
        var (avatar, mind, destination) = await PrepareAbilities();
        bool started = false;
        await Server.WaitPost(() =>
        {
            var settings = SEntMan.GetComponent<OrbitraRatvarEminenceAvatarComponent>(avatar);
            settings.RecallDelay = TimeSpan.FromSeconds(0.4);
            started = System.TryRecall((avatar, settings), destination);
            switch (invalidation)
            {
                case "move": Server.System<SharedTransformSystem>().SetCoordinates(_target, _origin.Offset(new Vector2(-1, 0))); break;
                case "power": Server.System<SharedTransformSystem>().Unanchor(destination); break;
                case "clear": System.ClearSelection((mind, SEntMan.GetComponent<OrbitraRatvarEminenceComponent>(mind))); break;
                case "foreign": SEntMan.GetComponent<OrbitraRatvarStructureComponent>(destination).Rule = null; break;
            }
        });
        await Pair.RunSeconds(0.8f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Energy, Is.EqualTo(1000));
            Assert.That(SEntMan.GetComponent<TransformComponent>(_target).Coordinates,
                Is.Not.EqualTo(SEntMan.GetComponent<TransformComponent>(destination).Coordinates));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarEminenceComponent>(mind).Recalling, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task MassRecallIsOncePerCultAndRevalidatesArk(bool destroyArk)
    {
        var (avatar, mind, destination) = await PrepareAbilities();
        bool first = false, duplicate = true;
        await Server.WaitPost(() =>
        {
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule);
            cult.Ark = SpawnBound(Ark, new Vector2(-1, 0));
            var settings = SEntMan.GetComponent<OrbitraRatvarEminenceAvatarComponent>(avatar);
            settings.MassRecallDelay = TimeSpan.FromSeconds(0.2);
            first = System.TryMassRecall((avatar, settings), destination);
            duplicate = System.TryMassRecall((avatar, settings), destination);
            if (destroyArk) SEntMan.QueueDeleteEntity(cult.Ark!.Value);
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(duplicate, Is.False);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).EminenceRecallUsed, Is.True);
            var moved = SEntMan.GetComponent<TransformComponent>(_target).Coordinates ==
                SEntMan.GetComponent<TransformComponent>(destination).Coordinates;
            Assert.That(moved, Is.EqualTo(!destroyArk));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Energy, Is.EqualTo(1000));
        });
    }

    [Test]
    public async Task RecallRejectsForeignCultAndHidesItsEndpoints()
    {
        var (avatar, _, destination) = await PrepareAbilities();
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(Rule, out var foreign);
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(foreign);
            cult.Station = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Station;
            cult.Energy = 1000;
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(destination).Rule = foreign;
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(System.CanRecall((avatar, SEntMan.GetComponent<OrbitraRatvarEminenceAvatarComponent>(avatar)),
                destination, out _, out _, out _), Is.False);
            Assert.That(System.BuildMenuState(avatar, avatar)!.Destinations, Is.Empty);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Energy, Is.EqualTo(1000));
        });
    }

    [Test]
    public async Task SummoningExpiredInvitationReleasesSlot()
    {
        await Prepare();
        EntityUid spire = default;
        bool started = false;
        await Server.WaitPost(() =>
        {
            spire = SpawnBound(Spire, new Vector2(0, 1));
            var beacon = SEntMan.GetComponent<OrbitraRatvarEminenceSpireComponent>(spire);
            beacon.ObjectionPeriod = TimeSpan.FromSeconds(0.1);
            beacon.InvitationDuration = TimeSpan.FromSeconds(0.2);
            started = System.TrySummon((spire, beacon), _target);
        });
        await Pair.RunSeconds(0.6f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(SEntMan.EntityQuery<OrbitraRatvarEminenceInvitationComponent>(), Is.Empty);
            Assert.That(System.CanSummon((spire, SEntMan.GetComponent<OrbitraRatvarEminenceSpireComponent>(spire)), _target, out _), Is.True);
        });
    }

    [TestCase(OrbitraRatvarEminenceReality.GridCheck)]
    [TestCase(OrbitraRatvarEminenceReality.Anomaly)]
    public async Task RealityWhitelistChargesOnceAndTargetsOnlyOwnStation(OrbitraRatvarEminenceReality effect)
    {
        var (avatar, _, _) = await PrepareAbilities();
        bool first = false, duplicate = true, forged = true;
        EntityUid station = default;
        EntityUid requestedEvent = default;
        await Server.WaitPost(() =>
        {
            station = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Station!.Value;
            SEntMan.EnsureComponent<StationEventEligibleComponent>(station);
            var otherStation = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationEventEligibleComponent>(otherStation);
            var settings = SEntMan.GetComponent<OrbitraRatvarEminenceAvatarComponent>(avatar);
            forged = System.TryManipulate((avatar, settings), (OrbitraRatvarEminenceReality) 255);
            first = System.TryManipulate((avatar, settings), effect);
            duplicate = System.TryManipulate((avatar, settings), OrbitraRatvarEminenceReality.Anomaly);
            // Штатное повторное StartGameRule пропускает только задержку объявления в тесте.
            var events = SEntMan.EntityQueryEnumerator<OrbitraRatvarEventTargetComponent>();
            events.MoveNext(out requestedEvent, out _);
            Server.System<GameTicker>().StartGameRule(requestedEvent);
        });
        await Pair.RunSeconds(0.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(forged, Is.False);
            Assert.That(first, Is.True);
            Assert.That(duplicate, Is.False);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_rule).Energy, Is.EqualTo(500));
            var targeted = SEntMan.EntityQuery<OrbitraRatvarEventTargetComponent>().Single();
            Assert.That(targeted.Station, Is.EqualTo(station));
            if (effect == OrbitraRatvarEminenceReality.GridCheck)
                Assert.That(SEntMan.GetComponent<PowerGridCheckRuleComponent>(requestedEvent).AffectedStation, Is.EqualTo(station));
            else
                Assert.That(SEntMan.GetComponent<AnomalySpawnRuleComponent>(requestedEvent).AnomalySpawnerPrototype.Id,
                    Is.EqualTo("OrbitraRatvarAnomalySpawner"));
        });
    }
}
