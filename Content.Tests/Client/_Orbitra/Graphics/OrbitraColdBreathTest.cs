using System.Numerics;
using System.Collections.Generic;
using Content.Client._Orbitra.Particles;
using Content.Shared._Orbitra.Particles;
using NUnit.Framework;
using Robust.Shared.Graphics.RSI;

namespace Content.Tests.Client._Orbitra.Graphics;

[TestFixture]
public sealed class OrbitraColdBreathTest
{
    [TestCase(RsiDirection.NorthEast, 1, 1)]
    [TestCase(RsiDirection.NorthWest, -1, 1)]
    [TestCase(RsiDirection.SouthEast, 1, -1)]
    [TestCase(RsiDirection.SouthWest, -1, -1)]
    public void DiagonalsDoNotFallBackToSouth(RsiDirection direction, int x, int y)
    {
        var forward = OrbitraParticleSystem.GetBreathForward(direction);
        Assert.That(forward.X * x, Is.GreaterThan(0));
        Assert.That(forward.Y * y, Is.GreaterThan(0));
        Assert.That(forward.Length(), Is.EqualTo(1).Within(0.0001));
        var mouth = OrbitraParticleSystem.GetMouth(direction, false, null, Vector2.One);
        Assert.That(mouth.X * x, Is.GreaterThan(0));
        Assert.That(mouth.Y * y, Is.GreaterThan(0));
    }

    [Test]
    public void HeadHidesRearFacingHumanoidBreath()
    {
        Assert.That(OrbitraParticleSystem.IsBreathFacingVisible(RsiDirection.North, true), Is.False);
        Assert.That(OrbitraParticleSystem.IsBreathFacingVisible(RsiDirection.South, true), Is.True);
        Assert.That(OrbitraParticleSystem.IsBreathFacingVisible(RsiDirection.North, false), Is.True);
    }

    [TestCase(293.15f, 101f, true, 0)]
    [TestCase(278.15f, 101f, true, 0)]
    [TestCase(268.15f, 101f, true, 8)]
    [TestCase(258.15f, 101f, true, 15)]
    [TestCase(200f, 101f, true, 15)]
    [TestCase(258.15f, 19.99f, true, 0)]
    [TestCase(258.15f, 20f, true, 15)]
    [TestCase(258.15f, 101f, false, 0)]
    [TestCase(null, 101f, true, 0)]
    [TestCase(float.NaN, 101f, true, 0)]
    public void AmbientGates(float? temperature, float pressure, bool breathing, int expected)
    {
        Assert.That(SharedOrbitraColdBreathSystem.Quantize(temperature, pressure, breathing), Is.EqualTo(expected));
    }

    [Test]
    public void IntensityHasSixteenMonotonicLevels()
    {
        var levels = new HashSet<byte>();
        byte previous = 0;
        for (var temperature = 280f; temperature >= 250; temperature -= 0.1f)
        {
            var level = SharedOrbitraColdBreathSystem.Quantize(temperature, 101, true);
            Assert.That(level, Is.GreaterThanOrEqualTo(previous));
            levels.Add(level);
            previous = level;
        }
        Assert.That(levels.Count, Is.EqualTo(16));
    }

    [Test]
    public void AnimalAnchorScalesAndHumanoidAnchorIsDirectional()
    {
        var small = OrbitraParticleSystem.GetMouth(RsiDirection.East, false, null, Vector2.One);
        var big = OrbitraParticleSystem.GetMouth(RsiDirection.East, false, null, Vector2.One * 2);
        Assert.That(big, Is.EqualTo(small * 2));
        var east = OrbitraParticleSystem.GetMouth(RsiDirection.East, true, null, Vector2.One);
        var west = OrbitraParticleSystem.GetMouth(RsiDirection.West, true, null, Vector2.One);
        Assert.That(east.X, Is.EqualTo(-west.X));
        Assert.That(east.Y, Is.EqualTo(west.Y));
        var profile = new OrbitraColdBreathVisualsComponent { East = new Vector2(0.21f, 0.03f) };
        Assert.That(OrbitraParticleSystem.GetMouth(RsiDirection.East, false, profile, Vector2.One), Is.EqualTo(profile.East));
    }
}
