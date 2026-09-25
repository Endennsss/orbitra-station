using System.Numerics;
using Content.Client._Orbitra.Particles;
using Content.Shared._Orbitra.Particles;
using NUnit.Framework;

namespace Content.Tests.Client._Orbitra.Graphics;

[TestFixture]
public sealed class OrbitraDustTest : OrbitraParticleTestBase
{
    [TestCase(-100, 100, 1, 32)]
    [TestCase(6, 6, 6, 6)]
    [TestCase(0, 0, 1, 1)]
    public void ZoneDimensionsClamp(int width, int height, int expectedWidth, int expectedHeight)
    {
        var bounds = OrbitraDust.Bounds(new Vector2(12, -4), width, height);
        Assert.That(bounds.Center, Is.EqualTo(new Vector2(12, -4)));
        Assert.That(bounds.Width, Is.EqualTo(expectedWidth));
        Assert.That(bounds.Height, Is.EqualTo(expectedHeight));
        Assert.That(OrbitraDust.Contains(new[] { bounds, bounds }, bounds.Center), Is.True);
        Assert.That(OrbitraDust.Contains(new[] { bounds }, bounds.TopRight + Vector2.One), Is.False);
    }

    [TestCase(0, 0, 0)]
    [TestCase(128, 4, 12)]
    [TestCase(384, 8, 32)]
    [TestCase(768, 12, 48)]
    public void AmbientBudgetIsPartOfPool(int capacity, int lamps, int motes)
    {
        Assert.That(OrbitraDust.AmbientBudget(capacity), Is.EqualTo((lamps, motes)));
        var pool = new OrbitraParticlePool();
        pool.Configure(capacity);
        for (var i = 0; i < 100; i++)
            pool.Add(new() { Effect = Effect("OrbitraTestAmbient"), Lifetime = 5 });
        Assert.That(pool.Count, Is.EqualTo(motes));
        Assert.That(pool.AmbientCount(), Is.EqualTo(motes));
    }

    [Test]
    public void SmokeAndWorkEvictAmbientBeforeOtherEffects()
    {
        var pool = new OrbitraParticlePool();
        pool.Configure(128);
        pool.Add(new() { Effect = Effect("OrbitraTestAmbient"), Lifetime = 5 });
        for (var i = 1; i < 128; i++)
            pool.Add(new() { Effect = Effect("OrbitraTestChip"), Lifetime = 1, Burst = true });
        Assert.That(pool.Add(new() { Effect = Effect("OrbitraTestSmoke"), Lifetime = 1 }), Is.True);
        Assert.That(pool.AmbientCount(), Is.Zero);
        Assert.That(pool.Count, Is.EqualTo(128));
        pool.Configure(0);
        Assert.That(pool.Count, Is.Zero);
    }

    [TestCase(4, 4, 0)]
    [TestCase(4, 1, 3)]
    [TestCase(-4, 0, 0)]
    public void ClosingSpeedIgnoresCommonMotion(float first, float second, float expected)
    {
        Assert.That(OrbitraDust.ClosingSpeed(new Vector2(first, 20), new Vector2(second, 20), Vector2.UnitX), Is.EqualTo(expected));
    }
}
