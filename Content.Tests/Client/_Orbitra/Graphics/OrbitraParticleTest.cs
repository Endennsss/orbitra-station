using System.Numerics;
using Content.Client._Orbitra.Particles;
using Content.Shared._Orbitra.Particles;
using NUnit.Framework;

namespace Content.Tests.Client._Orbitra.Graphics;

[TestFixture]
public sealed class OrbitraParticleTest : OrbitraParticleTestBase
{
    [TestCase("Off", 0, 0f)]
    [TestCase("Low", 128, 0.5f)]
    [TestCase("Medium", 384, 1f)]
    [TestCase("High", 768, 1.5f)]
    [TestCase("invalid", 384, 1f)]
    public void QualityIsBounded(string quality, int capacity, float density)
    {
        Assert.That(OrbitraParticleCVars.GetBudget(quality), Is.EqualTo((capacity, density)));
    }

    [TestCase(OrbitraParticleMaterial.Generic, "OrbitraParticleDust")]
    [TestCase(OrbitraParticleMaterial.Metal, "OrbitraParticleMetal")]
    [TestCase(OrbitraParticleMaterial.Wood, "OrbitraParticleWood")]
    [TestCase(OrbitraParticleMaterial.Stone, "OrbitraParticleStone")]
    [TestCase(OrbitraParticleMaterial.Glass, "OrbitraParticleGlass")]
    public void MaterialHasExplicitEffect(OrbitraParticleMaterial material, string effect)
    {
        Assert.That(OrbitraParticleCVars.MaterialEffect(material), Is.EqualTo(effect));
    }

    [Test]
    public void BurstReplacesSmokeBeforeOtherParticles()
    {
        var pool = new OrbitraParticlePool();
        pool.Configure(2);
        var chip = Effect("OrbitraTestChip");
        var smoke = Effect("OrbitraTestSmoke");
        pool.Add(new() { Effect = chip, Lifetime = 1 });
        pool.Add(new() { Effect = smoke, Lifetime = 1 });
        Assert.That(pool.Add(new() { Effect = smoke, Lifetime = 1 }), Is.False);
        Assert.That(pool.Add(new() { Effect = chip, Lifetime = 1, Burst = true }), Is.True);
        Assert.That(pool.Count, Is.EqualTo(2));
        Assert.That(pool.Particles[0].Burst, Is.False);
        Assert.That(pool.Particles[1].Burst, Is.True);
    }

    [Test]
    public void ContinuousWorkReplacesSmokeButNotAnotherWorkParticle()
    {
        var pool = new OrbitraParticlePool();
        pool.Configure(1);
        var smoke = Effect("OrbitraTestSmoke");
        var spark = Effect("OrbitraTestSpark");
        pool.Add(new() { Effect = smoke, Lifetime = 1 });
        Assert.That(pool.Add(new() { Effect = spark, Lifetime = 1 }), Is.True);
        Assert.That(pool.Particles[0].Effect, Is.SameAs(spark));
        Assert.That(pool.Add(new() { Effect = smoke, Lifetime = 1 }), Is.False);
        Assert.That(pool.Add(new() { Effect = spark, Lifetime = 1 }), Is.False);
    }

    [Test]
    public void RisingParticlesKeepMoreVelocityThanImpactDebris()
    {
        var pool = new OrbitraParticlePool();
        pool.Add(new() { Effect = Effect("OrbitraTestRising"), Lifetime = 1, Velocity = Vector2.UnitY });
        pool.Add(new() { Effect = Effect("OrbitraTestChip"), Lifetime = 1, Velocity = Vector2.UnitY });
        pool.Update(0.25f);
        Assert.That(pool.Particles[0].Velocity.Y, Is.GreaterThan(pool.Particles[1].Velocity.Y));
    }

    [Test]
    public void LifetimeDoesNotDependOnCameraOrSourceMovement()
    {
        var pool = new OrbitraParticlePool();
        pool.Add(new()
        {
            Effect = Effect("OrbitraTestChip"), Lifetime = 0.5f, Position = new Vector2(4, 7),
            Origin = new Vector2(4, 7), Velocity = Vector2.UnitX,
        });
        pool.Update(0.1f);
        Assert.That(pool.Particles[0].Position.X, Is.EqualTo(4.1f).Within(0.0001f));
        Assert.That(pool.Particles[0].Origin, Is.EqualTo(new Vector2(4, 7)));
        pool.Update(2f);
        Assert.That(pool.Count, Is.Zero);
    }

    [Test]
    public void DisablingClearsPoolAndRejectsBursts()
    {
        var pool = new OrbitraParticlePool();
        pool.Add(new() { Effect = Effect("OrbitraTestChip"), Lifetime = 1 });
        pool.Configure(0);
        Assert.That(pool.Count, Is.Zero);
        Assert.That(pool.Add(new() { Effect = Effect("OrbitraTestChip"), Lifetime = 1, Burst = true }), Is.False);
    }

    [Test]
    public void StressCannotExceedCapacity()
    {
        var pool = new OrbitraParticlePool();
        pool.Configure(128);
        var effect = Effect("OrbitraTestChip");
        for (var i = 0; i < 10000; i++)
            pool.Add(new() { Effect = effect, Lifetime = 0.5f, Burst = i % 7 == 0 });
        Assert.That(pool.Count, Is.EqualTo(128));
        pool.Update(1);
        Assert.That(pool.Count, Is.Zero);
    }

    [Test]
    public void RepeatedViewportDoesNotEmitAgainAndStallDoesNotAccumulate()
    {
        var fraction = 0f;
        Assert.That(OrbitraParticlePool.AdvanceEmission(ref fraction, 0.05f, 24), Is.EqualTo(1));
        Assert.That(OrbitraParticlePool.AdvanceEmission(ref fraction, 0, 24), Is.Zero);
        Assert.That(OrbitraParticlePool.AdvanceEmission(ref fraction, 60, 24), Is.EqualTo(1));
        Assert.That(fraction, Is.LessThan(1));
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(144)]
    public void EmissionRateDoesNotScaleWithFrameRate(int fps)
    {
        var fraction = 0f;
        var emitted = 0;
        for (var i = 0; i < fps; i++)
            emitted += OrbitraParticlePool.AdvanceEmission(ref fraction, 1f / fps, 24f);
        Assert.That(emitted + fraction, Is.EqualTo(24f).Within(0.001f));
    }
}
