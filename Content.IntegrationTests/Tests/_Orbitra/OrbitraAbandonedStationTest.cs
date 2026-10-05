using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.StationEvents;
using Content.Server._Orbitra.Cargo;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Fax;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents;
using Content.Server.StationEvents.Components;
using Content.Shared.Cargo;
using Content.Shared.AlertLevel;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.CCVar;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Fax;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Preferences;
using Content.Shared.Station.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Orbitra;

#pragma warning disable RA0002 // Подготовка изолированных тестовых состояний очереди и питания без изменения API игры.
public sealed class OrbitraAbandonedStationTest : GameTest
{
    private const string RuleId = "OrbitraTestAbandonedStation";
    private const string ProductionRuleId = "OrbitraAbandonedStation";
    private static readonly ProtoId<CargoProductPrototype> TestProduct = "FunCrateGambling";

    public override PoolSettings PoolSettings => new() { Dirty = true, DummyTicker = false };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: OrbitraTestAbandonedStation
          parent: OrbitraAbandonedStation
          components:
          - type: StationEvent
            earliestStart: 0
          - type: OrbitraAbandonedStationRule
            warningDuration: 0
            recoveryMinimum: 6000
            recoveryMaximum: 6000
            retryInterval: 2
            recoveryChance: 0

        - type: entity
          id: OrbitraTestAbandonedCryo
          components:
          - type: ContainerContainer
            containers:
              cryo: !type:ContainerSlot
          - type: ContainerSpawnPoint
            containerId: cryo
            spawnType: LateJoin
        """;

    private async Task<EntityUid> StartOutage()
    {
        EntityUid rule = default;
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.EmergencyShuttleAutoCallTime, 0);
            Server.System<GameTicker>().StartGameRule(RuleId, out rule);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(Server.System<OrbitraAbandonedStationRuleSystem>().IsIsolationActive, Is.True));
        return rule;
    }

    private EntityUid AddStation(EntityUid grid)
    {
        var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        SEntMan.AddComponent<StationDataComponent>(station);
        SEntMan.AddComponent<StationEventEligibleComponent>(station);
        SEntMan.AddComponent<StationSpawningComponent>(station);
        Server.System<StationSystem>().AddGridToStation(station, grid);
        return station;
    }

    [Test]
    public async Task ManualOnlyAndEarlyRejectionDoNotConsumeValidStart()
    {
        EntityUid rejected = default;
        await Server.WaitPost(() => Server.System<GameTicker>().StartGameRule(ProductionRuleId, out rejected));
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<EndedGameRuleComponent>(rejected), Is.True);
            Assert.That(Server.System<OrbitraAbandonedStationRuleSystem>().IsIsolationActive, Is.False);
            var prototype = SProtoMan.Index<EntityPrototype>(ProductionRuleId);
            Assert.That(prototype.TryComp<StationEventComponent>(out var config, SEntMan.ComponentFactory), Is.True);
            Assert.That(config.EarliestStart, Is.EqualTo(10));
            Assert.That(config.ManualOnly, Is.True);
            Assert.That(config.MaxOccurrences, Is.EqualTo(1));
            Assert.That(prototype.TryComp<OrbitraAbandonedStationRuleComponent>(out var eventConfig, SEntMan.ComponentFactory), Is.True);
            Assert.That(eventConfig.WarningDuration,
                Is.EqualTo(TimeSpan.FromSeconds(60)));
            Assert.That(Server.System<EventManagerSystem>().AvailableEvents(currentTimeOverride: TimeSpan.FromHours(2))
                .Keys.Any(p => p.ID == "OrbitraAbandonedStation" || p.ID == RuleId), Is.False);
        });

        var rule = await StartOutage();
        EntityUid duplicate = default;
        await Server.WaitPost(() => Server.System<GameTicker>().StartGameRule(RuleId, out duplicate));
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<EndedGameRuleComponent>(duplicate), Is.True);
            Assert.That(SEntMan.HasComponent<ActiveGameRuleComponent>(rule), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task IsolationAlertRestoresPreviousLevel(bool recover)
    {
        var map = await Pair.CreateTestMap();
        EntityUid station = default;
        await Server.WaitPost(() =>
        {
            station = AddStation(map.Grid);
            SEntMan.AddComponent<AlertLevelComponent>(station);
            Server.System<AlertLevelSystem>().SetLevel(station, "Blue", playSound: false, announce: false, force: true);
        });
        var rule = await StartOutage();
        await Server.WaitAssertion(() =>
        {
            var alert = SEntMan.GetComponent<AlertLevelComponent>(station);
            Assert.That(alert.CurrentAlertLevel.Id, Is.EqualTo("OrbitraIsolation"));
            Assert.That(Server.System<AlertLevelSystem>().CanChangeAlertLevel(station), Is.False);
            Assert.That(SProtoMan.Index<AlertLevelPrototype>(alert.CurrentAlertLevel).ForceEnableEmergencyLights, Is.True);
        });
        await Server.WaitPost(() =>
        {
            if (recover)
            {
                var comp = SEntMan.GetComponent<OrbitraAbandonedStationRuleComponent>(rule);
                comp.RecoveryAt = Server.System<GameTicker>().RoundDuration();
                comp.RecoveryChance = 1;
            }
            else
            {
                Server.System<GameTicker>().EndGameRule(rule);
            }
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var alert = SEntMan.GetComponent<AlertLevelComponent>(station);
            Assert.That(alert.CurrentAlertLevel.Id, Is.EqualTo("Blue"));
            Assert.That(alert.IsLevelLocked, Is.False);
            Assert.That(SEntMan.GetComponent<OrbitraAbandonedStationRuleComponent>(rule).PreviousAlerts, Is.Empty);
            Assert.That(Server.System<RoundEndSystem>().IsRoundEndRequested(), Is.EqualTo(recover));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task IsolationDoesNotOverrideEmergencyAlert(bool emergencyBeforeOutage)
    {
        var map = await Pair.CreateTestMap();
        EntityUid station = default;
        await Server.WaitPost(() =>
        {
            station = AddStation(map.Grid);
            SEntMan.AddComponent<AlertLevelComponent>(station);
            if (emergencyBeforeOutage)
                Server.System<AlertLevelSystem>().SetLevel(station, "DeltaNuke", playSound: false, announce: false, force: true);
        });
        var rule = await StartOutage();
        await Server.WaitPost(() =>
        {
            if (!emergencyBeforeOutage)
                Server.System<AlertLevelSystem>().SetLevel(station, "DeltaNuke", playSound: false, announce: false, force: true);
        });
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<AlertLevelComponent>(station).CurrentAlertLevel.Id, Is.EqualTo("DeltaNuke")));
        await Server.WaitPost(() => Server.System<GameTicker>().EndGameRule(rule));
        await Server.WaitAssertion(() =>
        {
            var alert = SEntMan.GetComponent<AlertLevelComponent>(station);
            Assert.That(alert.CurrentAlertLevel.Id, Is.EqualTo("DeltaNuke"));
            Assert.That(alert.IsLevelLocked, Is.True);
        });
    }

    [Test]
    public async Task TenMinuteBoundaryAndWarningDeadlineAreRespected()
    {
        EntityUid rule = default;
        int ticks = 0;
        await Server.WaitPost(() =>
        {
            rule = SEntMan.SpawnEntity(ProductionRuleId, MapCoordinates.Nullspace);
            // Убираем автоматический старт; проверяем тот же прототип через штатный StartGameRule.
            SEntMan.AddComponent<DelayedStartRuleComponent>(rule).RuleStartTime = TimeSpan.FromDays(1);
            var timing = Server.ResolveDependency<IGameTiming>();
            ticks = (int) Math.Ceiling((TimeSpan.FromMinutes(10) - Server.System<GameTicker>().RoundDuration()).TotalSeconds * timing.TickRate) - 1;
        });
        await Pair.RunTicksSync(ticks);
        await Server.WaitAssertion(() => Assert.That(Server.System<OrbitraAbandonedStationRuleSystem>().CanBegin(
            (rule, SEntMan.GetComponent<OrbitraAbandonedStationRuleComponent>(rule)), out _), Is.False));
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(Server.System<OrbitraAbandonedStationRuleSystem>().CanBegin(
            (rule, SEntMan.GetComponent<OrbitraAbandonedStationRuleComponent>(rule)), out _), Is.True));
        await Server.WaitPost(() => Server.System<GameTicker>().StartGameRule(rule));
        await Pair.RunSeconds(59);
        await Server.WaitAssertion(() => Assert.That(Server.System<OrbitraAbandonedStationRuleSystem>().IsIsolationActive, Is.False));
        await Pair.RunSeconds(1.1f);
        await Server.WaitAssertion(() => Assert.That(Server.System<OrbitraAbandonedStationRuleSystem>().IsIsolationActive, Is.True));
    }

    [Test]
    public async Task LateStartDefersRecoveryAndExistingEvacuationRejectsStart()
    {
        EntityUid rule = default;
        await Server.WaitPost(() =>
        {
            rule = Server.System<GameTicker>().AddGameRule(RuleId);
            var comp = SEntMan.GetComponent<OrbitraAbandonedStationRuleComponent>(rule);
            comp.RecoveryMinimum = TimeSpan.Zero;
            comp.RecoveryMaximum = TimeSpan.Zero;
            comp.RecoveryChance = 1;
            Server.System<GameTicker>().StartGameRule(rule);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<OrbitraAbandonedStationRuleComponent>(rule);
            Assert.That(comp.RecoveryAttempts, Is.Zero);
            Assert.That(comp.RecoveryAt, Is.GreaterThan(Server.System<GameTicker>().RoundDuration()));
        });
        await Pair.RunSeconds(3);
        EntityUid rejected = default;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().RestartRound();
            Server.System<RoundEndSystem>().RequestRoundEnd(checkCooldown: false);
            Server.System<GameTicker>().StartGameRule(RuleId, out rejected);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<EndedGameRuleComponent>(rejected), Is.True);
            Assert.That(Server.System<OrbitraAbandonedStationRuleSystem>().IsIsolationActive, Is.False);
        });
    }

    [Test]
    public async Task WarningWaitsAndEvacuationDuringWarningCancelsIsolation()
    {
        EntityUid rule = default;
        await Server.WaitPost(() =>
        {
            var ticker = Server.System<GameTicker>();
            rule = ticker.AddGameRule(RuleId);
            SEntMan.GetComponent<OrbitraAbandonedStationRuleComponent>(rule).WarningDuration = TimeSpan.FromSeconds(2);
            ticker.StartGameRule(rule);
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() => Assert.That(Server.System<OrbitraAbandonedStationRuleSystem>().IsIsolationActive, Is.False));
        await Server.WaitPost(() => Server.System<RoundEndSystem>().RequestRoundEnd(checkCooldown: false));
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<EndedGameRuleComponent>(rule), Is.True);
            Assert.That(Server.System<OrbitraAbandonedStationRuleSystem>().IsIsolationActive, Is.False);
            Assert.That(Server.System<RoundEndSystem>().IsRoundEndRequested(), Is.True);
        });
    }

    [Test]
    public async Task FailedRecoveryRetriesThenCallsIrrevocableEvacuation()
    {
        var rule = await StartOutage();
        await Server.WaitPost(() =>
        {
            var comp = SEntMan.GetComponent<OrbitraAbandonedStationRuleComponent>(rule);
            comp.RecoveryAt = Server.System<GameTicker>().RoundDuration();
            Server.System<RoundEndSystem>().RequestRoundEnd(checkCooldown: false);
            Server.CfgMan.SetCVar(CCVars.EmergencyShuttleAutoCallTime, 1);
            Server.System<RoundEndSystem>().AutoCallStartTime = Server.ResolveDependency<IGameTiming>().CurTime - TimeSpan.FromMinutes(2);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<OrbitraAbandonedStationRuleComponent>(rule);
            Assert.That(comp.RecoveryAttempts, Is.EqualTo(1));
            Assert.That(comp.RecoveryAt, Is.GreaterThan(Server.System<GameTicker>().RoundDuration()));
            Assert.That(Server.System<RoundEndSystem>().IsRoundEndRequested(), Is.False);
        });
        await Server.WaitPost(() => SEntMan.GetComponent<OrbitraAbandonedStationRuleComponent>(rule).RecoveryChance = 1);
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<OrbitraAbandonedStationRuleComponent>(rule).RecoveryAttempts, Is.EqualTo(2));
            Assert.That(Server.System<OrbitraAbandonedStationRuleSystem>().IsIsolationActive, Is.False);
            Assert.That(Server.System<RoundEndSystem>().IsRoundEndRequested(), Is.True);
            Assert.That(Server.System<RoundEndSystem>().CantRecall, Is.True);
        });
    }

    [Test]
    public async Task ManualEndRestoresServicesAndNextRoundResetsLimit()
    {
        var rule = await StartOutage();
        await Server.WaitPost(() => Server.System<GameTicker>().EndGameRule(rule));
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<OrbitraAbandonedStationRuleSystem>().CanUseService(), Is.True);
            Assert.That(Server.System<RoundEndSystem>().IsRoundEndRequested(), Is.False);
            Assert.That(Server.System<OrbitraAbandonedStationRuleSystem>().CanBegin(
                (rule, SEntMan.GetComponent<OrbitraAbandonedStationRuleComponent>(rule)), out _), Is.False);
        });
        await Server.WaitPost(() => Server.System<GameTicker>().RestartRound());
        await Pair.RunTicksSync(5);
        await StartOutage();
    }

    [Test]
    public async Task FaxesStayLocalAndRejectDirectExternalDelivery()
    {
        var local = await Pair.CreateTestMap();
        var remote = await Pair.CreateTestMap();
        EntityUid sender = default;
        EntityUid receiver = default;
        EntityUid outside = default;
        await Server.WaitPost(() =>
        {
            AddStation(local.Grid);
            AddStation(remote.Grid);
            sender = SEntMan.SpawnEntity("FaxMachineBase", local.GridCoords);
            receiver = SEntMan.SpawnEntity("FaxMachineBase", local.GridCoords);
            outside = SEntMan.SpawnEntity("FaxMachineBase", remote.GridCoords);
        });
        var rule = await StartOutage();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<OrbitraAbandonedStationRuleSystem>();
            Assert.That(system.CanFax(receiver, SEntMan.GetComponent<DeviceNetworkComponent>(sender).Address), Is.True);
            Assert.That(system.CanFax(receiver, SEntMan.GetComponent<DeviceNetworkComponent>(outside).Address), Is.False);
            Assert.That(system.CanFax(outside, SEntMan.GetComponent<DeviceNetworkComponent>(sender).Address), Is.False);
            Assert.That(system.CanFax(receiver, null), Is.False);
        });
        await Server.WaitPost(() =>
        {
            var fax = Server.System<FaxSystem>();
            fax.Receive(receiver, new FaxPrintout("external", "test"));
            fax.Receive(receiver, new FaxPrintout("internal", "test"), SEntMan.GetComponent<DeviceNetworkComponent>(sender).Address);
        });
        await Server.WaitAssertion(() =>
        {
            var queue = SEntMan.GetComponent<FaxMachineComponent>(receiver).PrintingQueue;
            Assert.That(queue.Count, Is.EqualTo(1));
            Assert.That(queue.Peek().Content, Is.EqualTo("internal"));
        });
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().EndGameRule(rule);
            Server.System<FaxSystem>().Receive(receiver, new FaxPrintout("restored", "test"));
        });
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<FaxMachineComponent>(receiver).PrintingQueue.Count, Is.EqualTo(2)));
    }

    [Test]
    public async Task PurchasesDoNotSpendMoneyOrConsumeQueuedDeliveries()
    {
        var map = await Pair.CreateTestMap();
        EntityUid station = default;
        EntityUid console = default;
        EntityUid actor = default;
        EntityUid telepad = default;
        CargoOrderData order = default!;
        int balance = 0;
        await Server.WaitPost(() =>
        {
            station = AddStation(map.Grid);
            var database = SEntMan.AddComponent<StationCargoOrderDatabaseComponent>(station);
            order = new CargoOrderData(1, "FunCrateGambling", 2, "test", "test", "Cargo") { Approved = true };
            database.Orders["Cargo"] = new List<CargoOrderData> { order };
            var bank = SEntMan.AddComponent<StationBankAccountComponent>(station);
            bank.IncreasePerSecond = 0;
            balance = bank.Accounts["Cargo"];
            console = SEntMan.SpawnEntity("ComputerCargoOrders", map.GridCoords);
            actor = SEntMan.SpawnEntity(null, map.GridCoords);
            telepad = SEntMan.SpawnEntity(null, map.GridCoords);
            var tele = SEntMan.AddComponent<CargoTelepadComponent>(telepad);
            tele.CurrentOrders.Add(order);
            tele.Accumulator = 0;
        });
        var rule = await StartOutage();
        bool added = true;
        await Server.WaitPost(() =>
        {
            SEntMan.EventBus.RaiseLocalEvent(console, new CargoConsoleAddOrderMessage("test", "test", "FunCrateGambling", 1) { Actor = actor });
            SEntMan.EventBus.RaiseLocalEvent(console, new CargoConsoleApproveOrderMessage(1) { Actor = actor });
            added = Server.System<CargoSystem>().AddAndApproveOrder(station,
                SProtoMan.Index(TestProduct), 1, "test", "test", "test",
                SEntMan.GetComponent<StationCargoOrderDatabaseComponent>(station), "Cargo",
                (station, SEntMan.GetComponent<StationDataComponent>(station)));
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(added, Is.False);
            Assert.That(SEntMan.GetComponent<StationBankAccountComponent>(station).Accounts["Cargo"], Is.EqualTo(balance));
            Assert.That(SEntMan.GetComponent<StationCargoOrderDatabaseComponent>(station).AllOrders.Count(), Is.EqualTo(1));
            Assert.That(order.NumDispatched, Is.Zero);
            Assert.That(SEntMan.GetComponent<CargoTelepadComponent>(telepad).CurrentOrders, Does.Contain(order));
        });
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().EndGameRule(rule);
            SEntMan.GetComponent<CargoTelepadComponent>(telepad).CurrentState = CargoTelepadState.Idle;
            SEntMan.GetComponent<CargoTelepadComponent>(telepad).Accumulator = 0;
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(order.NumDispatched, Is.EqualTo(1)));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task LateJoinUsesCryoOrStationFallbackWithoutChangingPreference(bool hasCryo)
    {
        var map = await Pair.CreateTestMap();
        EntityUid station = default;
        EntityUid cryo = default;
        EntityUid? mob = null;
        HumanoidCharacterProfile profile = default!;
        await Server.WaitPost(() =>
        {
            profile = HumanoidCharacterProfile.DefaultWithSpecies().WithSpawnPriorityPreference(SpawnPriorityPreference.Arrivals);
            station = AddStation(map.Grid);
            var spawn = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<SpawnPointComponent>(spawn).SpawnType = SpawnPointType.LateJoin;
            if (hasCryo)
                cryo = SEntMan.SpawnEntity("OrbitraTestAbandonedCryo", map.GridCoords);
        });
        await StartOutage();
        await Server.WaitPost(() => mob = Server.System<StationSpawningSystem>().SpawnPlayerCharacterOnStation(station, null, profile));
        await Server.WaitAssertion(() =>
        {
            Assert.That(mob, Is.Not.Null);
            Assert.That(Server.System<StationSystem>().GetOwningStation(mob), Is.EqualTo(station));
            Assert.That(profile.SpawnPriority, Is.EqualTo(SpawnPriorityPreference.Arrivals));
            if (hasCryo)
                Assert.That(SEntMan.GetComponent<ContainerManagerComponent>(cryo).Containers["cryo"].ContainedEntities, Does.Contain(mob.Value));
        });
    }

    [Test]
    public async Task DestroyedTelepadPreservesPaidOrderUntilTradeStationReturns()
    {
        var map = await Pair.CreateTestMap();
        EntityUid station = default;
        EntityUid telepad = default;
        CargoOrderData order = default!;
        await Server.WaitPost(() =>
        {
            station = AddStation(map.Grid);
            SEntMan.AddComponent<StationCargoOrderDatabaseComponent>(station);
            telepad = SEntMan.SpawnEntity(null, map.GridCoords);
            order = new CargoOrderData(1, TestProduct, 1, "test", "test", "Cargo") { Approved = true };
            SEntMan.AddComponent<CargoTelepadComponent>(telepad).CurrentOrders.Add(order);
        });
        var rule = await StartOutage();
        await Server.WaitPost(() => SEntMan.DeleteEntity(telepad));
        await Server.WaitAssertion(() =>
        {
            Assert.That(order.NumDispatched, Is.Zero);
            Assert.That(SEntMan.GetComponent<OrbitraSuspendedCargoDeliveryComponent>(station).Orders, Does.Contain(order));
        });
        await Server.WaitPost(() => Server.System<GameTicker>().EndGameRule(rule));
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() => Assert.That(order.NumDispatched, Is.Zero, "No trade station is available yet."));
        await Server.WaitPost(() =>
        {
            SEntMan.AddComponent<TradeStationComponent>(map.Grid);
            SEntMan.SpawnEntity("CargoPalletBuy", map.GridCoords);
        });
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(order.NumDispatched, Is.EqualTo(1));
            Assert.That(SEntMan.HasComponent<OrbitraSuspendedCargoDeliveryComponent>(station), Is.False);
        });
    }

    [Test]
    public async Task ArrivalsKeepEntitiesAndRestartScheduleAfterManualEnd()
    {
        var map = await Pair.CreateTestMap();
        EntityUid shuttle = default;
        EntityUid passenger = default;
        await Server.WaitPost(() =>
        {
            shuttle = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<ArrivalsShuttleComponent>(shuttle);
            passenger = SEntMan.SpawnEntity(null, new EntityCoordinates(shuttle, 0, 0));
        });
        var rule = await StartOutage();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<ArrivalsSystem>().NextShuttleArrival(), Is.Null);
            Assert.That(SEntMan.EntityExists(shuttle), Is.True);
            Assert.That(SEntMan.EntityExists(passenger), Is.True);
        });
        await Server.WaitPost(() => Server.System<GameTicker>().EndGameRule(rule));
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<ArrivalsShuttleComponent>(shuttle).NextTransfer,
            Is.GreaterThan(Server.ResolveDependency<IGameTiming>().CurTime)));
    }
}
