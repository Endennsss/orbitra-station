// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Linq;
using Content.Server._Orbitra.Research.Bluespace;
using Content.Server.Power.Components;
using Content.Shared._Orbitra.Research.Bluespace;
using Content.Shared.AlertLevel;
using Content.Shared.Anomaly.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.Tests._Orbitra.Research;

public sealed partial class OrbitraBluespaceInfrastructureTest
{
    /// <summary>Halved power and batch energy preserve the real full-power production cadence.</summary>
    [TestCase(OrbitraBluespacePowerMode.Normal, 60)]
    [TestCase(OrbitraBluespacePowerMode.Overdrive, 30)]
    public async Task HarvesterRetainsFullPowerBatchCadence(OrbitraBluespacePowerMode mode, int seconds)
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var harvester = await SpawnHarvester(context);
            var system = server.System<OrbitraBluespaceHarvesterSystem>();
            await server.WaitPost(() =>
            {
                OpenOutput(context);
                // Orbitra-Edit: проверяем заряд вручную, чтобы автоматическая выдача не скрыла момент готовности.
                harvester.Comp.NextOutputCheck = TimeSpan.MaxValue;
                if (mode == OrbitraBluespacePowerMode.Overdrive)
                {
                    system.TrySetPowerMode(harvester, mode, context.User);
                    system.Update(1f);
                }
                server.EntMan.GetComponent<PowerConsumerComponent>(harvester).NetworkLoad.ReceivingPower = 1_000_000f;
                for (var second = 0; second < seconds - 1; second++)
                    system.Update(1f);
            });
            await server.WaitAssertion(() => Assert.That(system.CanProduceReward(harvester, out _), Is.False));
            var produced = false;
            await server.WaitPost(() =>
            {
                system.Update(1f);
                produced = system.TryProduceReward(harvester);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(produced, Is.True);
                Assert.That(harvester.Comp.StoredEnergy, Is.Zero);
                Assert.That(RewardItems(context), Is.Positive);
            });
        });
    }

    /// <summary>Background cannot replace higher primary codes or bypass special protocol locks.</summary>
    [TestCase("Red", false)]
    [TestCase("Green", true)]
    [TestCase("Yellow", false)]
    [TestCase("DeltaNuke", false)]
    [TestCase("Epsilon", false)]
    public async Task BackgroundPreservesHigherAndLockedAlerts(string initial, bool locked)
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var alertLevel = server.System<AlertLevelSystem>();
            string previous = string.Empty;
            await server.WaitPost(() =>
            {
                var alerts = server.EntMan.GetComponent<AlertLevelComponent>(context.FirstStation);
                alertLevel.SetLevel((context.FirstStation, alerts), "Blue", playSound: false, announce: false, force: true);
                alertLevel.SetLevel((context.FirstStation, alerts), initial, playSound: false, announce: false, force: true, locked: locked);
                previous = alerts.CurrentAlertLevel.Id;
                server.System<OrbitraBluespaceBackgroundSystem>().TryAdjustLevel(context.FirstGrid, 85f);
            });
            await server.WaitAssertion(() =>
            {
                var alerts = server.EntMan.GetComponent<AlertLevelComponent>(context.FirstStation);
                Assert.That(alerts.CurrentAlertLevel.Id, Is.EqualTo(previous));
                Assert.That(alertLevel.TryGetLevel((context.FirstStation, alerts), out var level) && level == initial, Is.True);
                Assert.That(alerts.IsLevelLocked, Is.EqualTo(locked));
            });
        });
    }

    /// <summary>Small threshold oscillations must not repeatedly override a manual alert decision.</summary>
    [Test]
    public async Task BlueEscalationRearmsOnlyAfterBackgroundRecovery()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var background = server.System<OrbitraBluespaceBackgroundSystem>();
            await server.WaitPost(() =>
            {
                var alerts = server.EntMan.GetComponent<AlertLevelComponent>(context.FirstStation);
                background.TryAdjustLevel(context.FirstGrid, 70f);
                server.System<AlertLevelSystem>().SetLevel((context.FirstStation, alerts), "Green",
                    playSound: false, announce: false, force: true);
                background.TryAdjustLevel(context.FirstGrid, -1f);
                background.TryAdjustLevel(context.FirstGrid, 1f);
            });
            await server.WaitAssertion(() => Assert.That(
                server.EntMan.GetComponent<AlertLevelComponent>(context.FirstStation).CurrentAlertLevel.Id, Is.EqualTo("Green")));
            await server.WaitPost(() =>
            {
                background.TryAdjustLevel(context.FirstGrid, -11f);
                background.TryAdjustLevel(context.FirstGrid, 11f);
            });
            await server.WaitAssertion(() => Assert.That(
                server.EntMan.GetComponent<AlertLevelComponent>(context.FirstStation).CurrentAlertLevel.Id, Is.EqualTo("Blue")));
        });
    }

    /// <summary>Native anomalies have a separate station budget and persist after the machinery is switched off.</summary>
    [Test]
    public async Task HarvesterAnomaliesRespectThresholdCooldownAndLifetime()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var harvester = await SpawnHarvester(context);
            var system = server.System<OrbitraBluespaceHarvesterSystem>();
            var background = server.System<OrbitraBluespaceBackgroundSystem>();
            var spawned = true;
            var repeated = true;
            await server.WaitPost(() =>
            {
                OpenOutput(context);
                background.TryAdjustLevel(harvester, 84f);
                spawned = system.TrySpawnAnomaly(harvester);
            });
            await server.WaitAssertion(() => Assert.That(spawned, Is.False));
            await server.WaitPost(() =>
            {
                background.TryAdjustLevel(harvester, 1f);
                spawned = system.TrySpawnAnomaly(harvester);
                repeated = system.TrySpawnAnomaly(harvester);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(spawned, Is.True);
                Assert.That(repeated, Is.False);
                var incidents = server.EntMan.GetComponent<OrbitraBluespaceHarvesterStationComponent>(context.FirstStation);
                Assert.That(incidents.NextAnomaly, Is.EqualTo(server.Timing.CurTime + TimeSpan.FromSeconds(120)));
                Assert.That(incidents.Incursions, Is.Empty);
                Assert.That(incidents.Anomalies, Has.Count.EqualTo(1));
                var anomaly = incidents.Anomalies.Single();
                Assert.That(server.EntMan.GetComponent<MetaDataComponent>(anomaly).EntityPrototype!.ID, Is.EqualTo("AnomalyBluespace"));
                Assert.That(server.EntMan.HasComponent<AnomalyComponent>(anomaly), Is.True);
                Assert.That(server.EntMan.HasComponent<TimedDespawnComponent>(anomaly), Is.False);
                Assert.That(server.EntMan.HasComponent<OrbitraBluespaceHarvesterStationComponent>(context.SecondStation), Is.False);
            });
            await server.WaitPost(() =>
            {
                var incidents = server.EntMan.GetComponent<OrbitraBluespaceHarvesterStationComponent>(context.FirstStation);
                incidents.NextAnomaly = server.Timing.CurTime;
                spawned = system.TrySpawnAnomaly(harvester);
            });
            await server.WaitAssertion(() => Assert.That(spawned, Is.False,
                "A second anomaly must not overlap the first when it occupies the only output tile."));
            await server.WaitPost(() =>
            {
                var incidents = server.EntMan.GetComponent<OrbitraBluespaceHarvesterStationComponent>(context.FirstStation);
                server.System<SharedMapSystem>().SetTile(context.FirstGrid,
                    new EntityCoordinates(context.FirstGrid, 0.5f, -1.5f), context.Floor);
                spawned = system.TrySpawnAnomaly(harvester);
                incidents.NextAnomaly = server.Timing.CurTime;
                repeated = system.TrySpawnAnomaly(harvester);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(spawned, Is.True);
                Assert.That(repeated, Is.False);
                Assert.That(server.EntMan.GetComponent<OrbitraBluespaceHarvesterStationComponent>(context.FirstStation).Anomalies,
                    Has.Count.EqualTo(2));
            });
            await server.WaitPost(() =>
            {
                var incidents = server.EntMan.GetComponent<OrbitraBluespaceHarvesterStationComponent>(context.FirstStation);
                server.EntMan.DeleteEntity(incidents.Anomalies.First());
                spawned = system.TrySpawnAnomaly(harvester);
                system.TrySetEnabled(harvester, false, context.User);
                background.TryAdjustLevel(harvester, -85f);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(spawned, Is.True, "Neutralized anomalies release budget for a later spawn.");
                var incidents = server.EntMan.GetComponent<OrbitraBluespaceHarvesterStationComponent>(context.FirstStation);
                Assert.That(incidents.Anomalies, Has.Count.EqualTo(2));
                Assert.That(incidents.Anomalies.All(uid => server.EntMan.HasComponent<AnomalyComponent>(uid)), Is.True);
                Assert.That(system.CanSpawnAnomaly(harvester, out _, out _), Is.False);
            });
        });
    }

    /// <summary>Dead creatures release capacity for the next wave.</summary>
    [Test]
    public async Task IncursionBudgetExcludesDeadCreatures()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var harvester = await SpawnHarvester(context);
            var system = server.System<OrbitraBluespaceHarvesterSystem>();
            var spawned = false;
            await server.WaitPost(() =>
            {
                OpenOutput(context);
                server.System<OrbitraBluespaceBackgroundSystem>().TryAdjustLevel(harvester, 85f);
                system.TryOpenIncursion(harvester);
                var incidents = server.EntMan.GetComponent<OrbitraBluespaceHarvesterStationComponent>(context.FirstStation);
                incidents.NextIncident = server.Timing.CurTime;
                system.TryOpenIncursion(harvester);
                foreach (var creature in incidents.Incursions.Take(2))
                    server.System<MobStateSystem>().ChangeMobState(creature, MobState.Dead);
                incidents.NextIncident = server.Timing.CurTime;
                spawned = system.TryOpenIncursion(harvester);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(spawned, Is.True);
                Assert.That(server.EntMan.GetComponent<OrbitraBluespaceHarvesterStationComponent>(context.FirstStation).Incursions,
                    Has.Count.EqualTo(6));
            });
        });
    }

    /// <summary>Real entity initialization callbacks cannot recursively spend either station hazard budget.</summary>
    [Test]
    public async Task SpawnCallbacksCannotBypassSharedStationLimits()
    {
        await WithStations(async context =>
        {
            var server = context.Pair.Server;
            var harvester = await SpawnHarvester(context);
            var system = server.System<OrbitraBluespaceHarvesterSystem>();
            var nested = false;
            var callbacks = 0;
            var spawned = false;
            await server.WaitPost(() =>
            {
                OpenOutput(context);
                server.System<OrbitraBluespaceBackgroundSystem>().TryAdjustLevel(harvester, 85f);
                void OnInitialized(Entity<MetaDataComponent> ent)
                {
                    if (ent.Comp.EntityPrototype?.ID is not ("OrbitraBluespaceRift" or "AnomalyBluespace"))
                        return;

                    callbacks++;
                    nested |= system.TryOpenIncursion(harvester) | system.TrySpawnAnomaly(harvester);
                }

                server.EntMan.EntityInitialized += OnInitialized;
                try
                {
                    spawned = system.TryOpenIncursion(harvester) && system.TrySpawnAnomaly(harvester);
                }
                finally
                {
                    server.EntMan.EntityInitialized -= OnInitialized;
                }
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(spawned, Is.True);
                Assert.That(callbacks, Is.EqualTo(2));
                Assert.That(nested, Is.False);
                var incidents = server.EntMan.GetComponent<OrbitraBluespaceHarvesterStationComponent>(context.FirstStation);
                Assert.That(incidents.Incursions, Has.Count.EqualTo(3));
                Assert.That(incidents.Anomalies, Has.Count.EqualTo(1));
                Assert.That(incidents.Spawning, Is.False);
            });
        });
    }
}
