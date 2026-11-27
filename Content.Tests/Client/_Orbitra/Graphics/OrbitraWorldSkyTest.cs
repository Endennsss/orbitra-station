using System;
using Content.Client._Orbitra.Parallax;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Timing;
using NUnit.Framework;
using Robust.Shared.Map;

namespace Content.Tests.Client._Orbitra.Graphics;

[TestFixture]
public sealed class OrbitraWorldSkyTest
{
    [Test]
    public void MapPatternCanBeRecoveredWithoutThePreviousViewport()
    {
        var first = OrbitraSkyRenderer.GetMapSeed(new MapId(1));
        var second = OrbitraSkyRenderer.GetMapSeed(new MapId(2));
        Assert.That(second, Is.Not.EqualTo(first));
        Assert.That(OrbitraSkyRenderer.GetMapSeed(new MapId(1)), Is.EqualTo(first));
    }

    [TestCase(0.8)]
    [TestCase(10.0)]
    public void FlightStartsAtRestAndMeetsArrivalWithoutAJump(double travelSeconds)
    {
        var start = TimeSpan.FromSeconds(120);
        var arriving = start + TimeSpan.FromSeconds(travelSeconds);
        var end = arriving + TimeSpan.FromSeconds(4);
        var flight = new FTLComponent { State = FTLState.Starting, StateTime = new StartEndTime(start, arriving) };
        Assert.That(OrbitraWarpMotion.Sample(flight, start).Speed, Is.Zero);

        flight.State = FTLState.Travelling;
        var departure = OrbitraWarpMotion.Sample(flight, start);
        var middle = OrbitraWarpMotion.Sample(flight, start + TimeSpan.FromSeconds(travelSeconds / 2));
        var cruise = OrbitraWarpMotion.Sample(flight, arriving);
        Assert.That(departure.Speed, Is.Zero);
        Assert.That(middle.Speed, Is.EqualTo(1f));
        Assert.That(middle.Distance, Is.GreaterThan(departure.Distance));
        Assert.That(cruise.Distance, Is.GreaterThan(middle.Distance));
        Assert.That(OrbitraWarpMotion.Sample(flight, start), Is.EqualTo(departure));

        flight.State = FTLState.Arriving;
        flight.StateTime = new StartEndTime(arriving, end);
        Assert.That(OrbitraWarpMotion.Sample(flight, arriving), Is.EqualTo(cruise));
        Assert.That(OrbitraWarpMotion.Sample(flight, arriving + TimeSpan.FromSeconds(2)).Speed, Is.EqualTo(0.5f));
        var stopped = OrbitraWarpMotion.Sample(flight, end);
        Assert.That(stopped.Speed, Is.Zero);
        Assert.That(stopped.Distance, Is.GreaterThan(cruise.Distance));
        Assert.That(OrbitraWarpMotion.Sample(flight, end + TimeSpan.FromSeconds(1)), Is.EqualTo(stopped));
    }
}
