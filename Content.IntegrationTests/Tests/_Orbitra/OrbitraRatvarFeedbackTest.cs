using System;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Doors.Components;
using Content.Shared.GameTicking;
using Content.Shared.Prying.Systems;
using Content.Shared.Station.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Native marauder prying, frozen shield and manifestation lifetime regressions.</summary>
[TestFixture]
public sealed class OrbitraRatvarFeedbackTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [Test]
    public async Task MarauderPrysDoorAndShieldRemainsStatic()
    {
        var map = await Pair.CreateTestMap();
        EntityUid door = default;
        var started = false;
        await Server.WaitPost(() =>
        {
            var body = SEntMan.SpawnEntity("OrbitraRatvarMarauder", map.GridCoords.Offset(new Vector2(-0.7f, 0)));
            door = SEntMan.SpawnEntity("Airlock", map.GridCoords);
            Server.System<PryingSystem>().TryPry(door, body, out var operation, body);
            started = operation != null;
        });
        await Server.WaitAssertion(() => Assert.That(started, Is.True));
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Open)));
        var frozen = false;
        await Client.WaitPost(() =>
        {
            var body = CEntMan.SpawnEntity("OrbitraRatvarMarauder", MapCoordinates.Nullspace);
            var sprite = CEntMan.GetComponent<SpriteComponent>(body);
            frozen = Client.System<SpriteSystem>().TryGetLayer((body, sprite), OrbitraRatvarMarauderLayers.Shield, out var layer, false) && !layer.AutoAnimated;
            CEntMan.DeleteEntity(body);
        });
        Assert.That(frozen, Is.True);
    }

    [Test]
    public async Task ManifestationSurvivesFinalDeadlineAndPostRoundButNotRestart()
    {
        var map = await Pair.CreateTestMap();
        EntityUid god = default;
        OrbitraRatvarRuleComponent cult = default!;
        await Server.WaitPost(() =>
        {
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationDataComponent>(station);
            Server.System<StationSystem>().AddGridToStation(station, map.Grid);
            Server.System<GameTicker>().StartGameRule("OrbitraRatvarRule", out var rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Station = station;
            cult.Won = true;
            cult.FinishAt = Server.Resolve<IGameTiming>().CurTime;
            god = SEntMan.SpawnEntity("OrbitraRatvarFinale", map.GridCoords);
            cult.Manifestation = god;
            var manifestation = SEntMan.GetComponent<OrbitraRatvarManifestationComponent>(god);
            manifestation.Rule = rule;
            manifestation.Grid = map.Grid;
            manifestation.Map = map.MapUid;
            Server.System<OrbitraRatvarRuleSystem>().Update(0);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GameRunLevelChangedEvent(GameRunLevel.InRound, GameRunLevel.PostRound));
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(god), Is.True);
            Assert.That(cult.FinishAt, Is.Null);
            Assert.That(Server.System<OrbitraRatvarManifestationSystem>().CanAct((god, SEntMan.GetComponent<OrbitraRatvarManifestationComponent>(god)), out _), Is.True);
        });
        await Server.WaitPost(() => SEntMan.EventBus.RaiseEvent(EventSource.Local, new GameRunLevelChangedEvent(GameRunLevel.PostRound, GameRunLevel.PreRoundLobby)));
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.EntityExists(god), Is.False));
    }
}
