using Content.Client._Orbitra.Shaders.DarkStation;
using Content.Shared._Orbitra.Graphics;
using NUnit.Framework;

namespace Content.Tests.Client._Orbitra.Graphics;

[TestFixture]
[TestOf(typeof(OrbitraDarkStationCVars))]
public sealed class OrbitraDarkStationTest
{
    [Test]
    public void DefaultsEnableDarkStationWithSubtleStrength()
    {
        Assert.That(OrbitraDarkStationCVars.Enabled.DefaultValue, Is.True);
        Assert.That(OrbitraDarkStationCVars.Strength.DefaultValue, Is.EqualTo(0.65f));
    }

    [TestCase(-1f, 0f)]
    [TestCase(0f, 0f)]
    [TestCase(0.65f, 0.65f)]
    [TestCase(1f, 1f)]
    [TestCase(2f, 1f)]
    [TestCase(float.NaN, 0f)]
    [TestCase(float.PositiveInfinity, 0f)]
    [TestCase(float.NegativeInfinity, 0f)]
    public void StrengthIsClampedToSafeRange(float configured, float expected)
    {
        Assert.That(OrbitraDarkStationCVars.ClampStrength(configured), Is.EqualTo(expected));
    }

    [Test]
    public void DrawOrderKeepsWorldGradeBetweenParticlesAndOptics()
    {
        Assert.That(OrbitraDarkStationOverlay.DrawOrder, Is.GreaterThan(150));
        Assert.That(OrbitraDarkStationOverlay.DrawOrder, Is.LessThan(200));
        Assert.That(OrbitraDarkStationOverlay.DrawOrder, Is.LessThan(300));
    }
}
