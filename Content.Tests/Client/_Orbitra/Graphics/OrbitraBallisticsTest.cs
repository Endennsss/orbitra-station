using System.Numerics;
using Content.Client._Orbitra.Particles;
using Content.Shared._Orbitra.Particles;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Client._Orbitra.Graphics;

[TestFixture]
public sealed class OrbitraBallisticsTest : OrbitraParticleTestBase
{
    [TestCase(OrbitraParticleMaterial.Generic, "OrbitraParticleBulletDust")]
    [TestCase(OrbitraParticleMaterial.Metal, "OrbitraParticleBulletMetal")]
    [TestCase(OrbitraParticleMaterial.Stone, "OrbitraParticleBulletStone")]
    [TestCase(OrbitraParticleMaterial.Wood, "OrbitraParticleBulletWood")]
    [TestCase(OrbitraParticleMaterial.Glass, "OrbitraParticleBulletGlass")]
    public void MaterialSelectsBallisticPreset(OrbitraParticleMaterial material, string effect) =>
        Assert.That(OrbitraBallistics.Effect(material), Is.EqualTo(effect));

    [TestCase(0, 0)]
    [TestCase(128, 16)]
    [TestCase(384, 48)]
    [TestCase(768, 96)]
    public void MarksRespectQualityBudget(int capacity, int expected)
    {
        var pool = new OrbitraParticlePool();
        pool.Configure(capacity);
        for (var i = 0; i < 120; i++)
            pool.Add(Mark(i + 1, Vector2.Zero));
        Assert.That(pool.Count, Is.EqualTo(expected));
    }

    [Test]
    public void MarksLimitEachObjectAndRejectNearbyDuplicates()
    {
        var pool = new OrbitraParticlePool();
        Assert.That(pool.Add(Mark(1, Vector2.Zero)), Is.True);
        Assert.That(pool.Add(Mark(1, new Vector2(0.01f, 0))), Is.False);
        for (var i = 1; i < 8; i++)
            pool.Add(Mark(1, new Vector2(i, 0)));
        Assert.That(pool.Count, Is.EqualTo(4));
        pool.Update(21);
        Assert.That(pool.Count, Is.Zero);
    }

    [Test]
    public void QualityReductionAndOffClearExcessMarks()
    {
        var pool = new OrbitraParticlePool();
        for (var i = 0; i < 48; i++)
            pool.Add(Mark(i + 1, Vector2.Zero));
        pool.Configure(128);
        Assert.That(pool.Count, Is.EqualTo(16));
        pool.Configure(0);
        Assert.That(pool.Count, Is.Zero);
    }

    [Test]
    public void WorkingParticlesCanDisplaceMarks()
    {
        var pool = new OrbitraParticlePool();
        pool.Configure(1);
        pool.Add(Mark(1, Vector2.Zero));
        Assert.That(pool.Add(new() { Effect = Effect("OrbitraTestChip"), Lifetime = 1, Burst = true }), Is.True);
        Assert.That(pool.Particles[0].Effect.ImpactMark, Is.False);
    }

    private OrbitraParticlePool.Particle Mark(int source, Vector2 position) => new()
    {
        Effect = Effect("OrbitraTestMark"),
        ImpactSource = new EntityUid(source),
        Position = position,
        Lifetime = 20,
    };
}
