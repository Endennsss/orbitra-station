using System;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Lavaland;
using Content.Shared._Orbitra.Lavaland;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weather;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Orbitra.Lavaland;

public sealed class OrbitraAshFrontTest : GameTest
{
    [Test]
    public async Task ForecastPrecedesLocalizedWeatherAndTheNextFront()
    {
        var home = await Pair.CreateTestMap();
        var weather = SEntMan.System<SharedWeatherSystem>();
        var clock = Server.ResolveDependency<IGameTiming>();
        var map = home.MapUid;
        var maps = SEntMan.System<SharedMapSystem>();
        var tiles = Server.ResolveDependency<ITileDefinitionManager>();
        var statuses = SEntMan.System<StatusEffectsSystem>();
        var previous = Pair.Player!.AttachedEntity;
        EntityUid worker = default;
        await Server.WaitAssertion(() =>
        {
            var front = SEntMan.EnsureComponent<OrbitraAshFrontComponent>(map);
            front.Sites.Add(Direction.North, new Vector2(35, 90));
            Assert.That(front.NextChange, Is.GreaterThan(clock.CurTime));
            front.NextChange = clock.CurTime;
        });
        await Pair.RunTicksSync(75);
        await Server.WaitAssertion(() =>
        {
            var front = SEntMan.GetComponent<OrbitraAshFrontComponent>(map);
            Assert.That(front.Phase, Is.EqualTo(OrbitraAshPhase.Forecast));
            Assert.That(front.Sector, Is.EqualTo(Direction.North));
            Assert.That(front.NextChange - clock.CurTime, Is.GreaterThan(TimeSpan.FromSeconds(40)));
            Assert.That(weather.HasWeather(home.MapId, OrbitraAshFrontSystem.Weather), Is.False);
            front.NextChange = clock.CurTime;
        });
        await Pair.RunTicksSync(75);
        await Server.WaitAssertion(() =>
        {
            var front = SEntMan.GetComponent<OrbitraAshFrontComponent>(map);
            Assert.That(front.Phase, Is.EqualTo(OrbitraAshPhase.Passing));
            Assert.That(weather.HasWeather(home.MapId, OrbitraAshFrontSystem.Weather), Is.True);
            var band = SEntMan.GetComponent<OrbitraAshBandComponent>(front.WeatherEntity!.Value);
            var middle = band.Footprint(0.5f);
            Assert.That(middle.Strength(front.Sites[front.Sector]), Is.EqualTo(1));
            Assert.That(middle.Strength(Vector2.Zero), Is.Zero);
            Assert.That(band.Footprint(0f).Strength(middle.Center), Is.Zero);
            Assert.That(band.Footprint(1f).Strength(middle.Center), Is.Zero);
            Assert.That(middle.Strength(middle.Center + middle.Travel * 19f), Is.InRange(0.1f, 0.9f));
            maps.SetTile(home.Grid, Vector2i.Zero, new Tile(tiles["FloorBasalt"].TileId));
            worker = SEntMan.SpawnAttachedTo("MobHuman", new EntityCoordinates(home.Grid, 0.5f, 0.5f));
            Server.PlayerMan.SetAttachedEntity(Pair.Player!, worker);
            // Подводим ядро фронта к проверяемой клетке без ожидания реальных двух минут.
            var world = SEntMan.System<SharedTransformSystem>().GetWorldPosition(worker);
            band.Site = world + band.Travel * 60f;
            SEntMan.Dirty(front.WeatherEntity.Value, band);
        });
        await Pair.RunTicksSync(75);
        await Server.WaitAssertion(() =>
        {
            Assert.That(statuses.TryGetStatusEffect(worker, "OrbitraAshSlowdown", out _), Is.True);
            var roof = SEntMan.EnsureComponent<RoofComponent>(home.Grid);
            SEntMan.System<SharedRoofSystem>().SetRoof((home.Grid.Owner, home.Grid.Comp, roof), Vector2i.Zero, true);
        });
        await Pair.RunTicksSync(160);
        await Server.WaitAssertion(() =>
        {
            Assert.That(statuses.TryGetStatusEffect(worker, "OrbitraAshSlowdown", out _), Is.False);
            Server.PlayerMan.SetAttachedEntity(Pair.Player!, previous);
            SEntMan.DeleteEntity(worker);
            statuses.TryRemoveStatusEffect(map, OrbitraAshFrontSystem.Weather);
        });
        await Pair.RunTicksSync(75);
        await Server.WaitAssertion(() =>
        {
            var front = SEntMan.GetComponent<OrbitraAshFrontComponent>(map);
            Assert.That(front.Phase, Is.EqualTo(OrbitraAshPhase.Clear));
            Assert.That(front.WeatherEntity, Is.Null);
            Assert.That(front.NextChange - clock.CurTime, Is.GreaterThan(TimeSpan.FromMinutes(7)));
        });
    }
}
