using System;
using System.Numerics;
using Content.Client._Orbitra.Particles;
using NUnit.Framework;

namespace Content.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraBulletTrailPathTest
{
    [Test]
    public void FirstObservationAndStillnessProduceNoTail()
    {
        var path = new OrbitraBulletTrailPath();
        var position = new Vector2(12, -4);
        path.Observe(position, 1);
        path.Observe(position, 1);

        Assert.That(path.Count, Is.EqualTo(1));
        Assert.That(path.GetPoint(0), Is.EqualTo(position));
        Assert.That(path.Length, Is.Zero);
    }

    [Test]
    public void DenseStraightSamplesKeepTheObservedLength()
    {
        var path = new OrbitraBulletTrailPath();
        path.Reset(Vector2.Zero);
        for (var i = 1; i <= 256; i++)
            path.Observe(new Vector2(i / 1024f, i / 2048f), 1);

        var head = new Vector2(0.25f, 0.125f);
        Assert.That(path.Count, Is.EqualTo(2));
        Assert.That(path.GetPoint(0), Is.EqualTo(Vector2.Zero));
        Assert.That(path.GetPoint(1), Is.EqualTo(head));
        Assert.That(path.Length, Is.EqualTo(head.Length()).Within(0.00001f));
    }

    [Test]
    public void DenseCurvedSamplesPreserveTheArc()
    {
        var path = new OrbitraBulletTrailPath();
        const float radius = 0.5f;
        for (var i = 0; i <= 120; i++)
        {
            var angle = i * (MathF.PI / 2 / 120);
            path.Observe(radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle)), 1);
        }

        Assert.That(path.GetPoint(0), Is.EqualTo(new Vector2(radius, 0)));
        Assert.That(path.Count, Is.InRange(3, 16));
        Assert.That(path.Length, Is.EqualTo(radius * MathF.PI / 2).Within(0.005f));
    }

    [Test]
    public void CroppingAFoldedPathKeepsTheRicochetAndCorner()
    {
        var path = new OrbitraBulletTrailPath();
        path.Reset(Vector2.Zero);
        path.Observe(new Vector2(0.5f, 0), 1);
        path.Observe(new Vector2(0.5f, 0.25f), 1);
        path.Observe(new Vector2(0.5f, 0.125f), 0.625f);

        Assert.That(path.Count, Is.EqualTo(4));
        Assert.That(path.GetPoint(0), Is.EqualTo(new Vector2(0.25f, 0)));
        Assert.That(path.GetPoint(1), Is.EqualTo(new Vector2(0.5f, 0)));
        Assert.That(path.GetPoint(2), Is.EqualTo(new Vector2(0.5f, 0.25f)));
        Assert.That(path.GetPoint(3), Is.EqualTo(new Vector2(0.5f, 0.125f)));
        Assert.That(path.Length, Is.EqualTo(0.625f));
    }

    [Test]
    public void CrossingManyBendsKeepsTheNewestSamplesInOrder()
    {
        var path = new OrbitraBulletTrailPath();
        for (var i = 0; i < 64; i++)
            path.Observe(new Vector2(i, i % 2), 1000);

        Assert.That(path.Count, Is.InRange(3, 16));
        for (var i = 0; i < path.Count; i++)
        {
            var original = 64 - path.Count + i;
            Assert.That(path.GetPoint(i), Is.EqualTo(new Vector2(original, original % 2)));
        }
    }

    [Test]
    public void ReducingLengthWhileStationaryTrimsExistingHistory()
    {
        var path = new OrbitraBulletTrailPath();
        path.Reset(Vector2.Zero);
        path.Observe(Vector2.UnitX, 1);
        path.Observe(Vector2.UnitX, 0.25f);

        Assert.That(path.Count, Is.EqualTo(2));
        Assert.That(path.GetPoint(0), Is.EqualTo(new Vector2(0.75f, 0)));
        Assert.That(path.Length, Is.EqualTo(0.25f));
    }

    [Test]
    public void ResetAndZeroLengthDiscardOldPositions()
    {
        var path = new OrbitraBulletTrailPath();
        path.Reset(Vector2.Zero);
        path.Observe(Vector2.One, 1);
        path.Reset(Vector2.UnitY);
        Assert.That(path.Count, Is.EqualTo(1));
        Assert.That(path.Length, Is.Zero);

        path.Observe(Vector2.Zero, 0);
        path.Observe(new Vector2(0.25f, 0), 1);
        Assert.That(path.GetPoint(0), Is.EqualTo(Vector2.Zero));
        Assert.That(path.Length, Is.EqualTo(0.25f));
    }

    [Test]
    public void ReusingOnePathLeavesOtherProjectilesUnchanged()
    {
        var first = new OrbitraBulletTrailPath();
        var second = new OrbitraBulletTrailPath();
        first.Reset(Vector2.UnitY);
        second.Reset(Vector2.UnitX);
        second.Observe(Vector2.One, 1);
        first.Reset(Vector2.Zero);

        Assert.That(second.GetPoint(0), Is.EqualTo(Vector2.UnitX));
        Assert.That(second.GetPoint(1), Is.EqualTo(Vector2.One));
        Assert.That(second.Length, Is.EqualTo(1));
    }

    [TestCase(-5, false, false, 0)]
    [TestCase(100, false, false, 32)]
    [TestCase(10, false, false, 10)]
    [TestCase(9, true, false, 5)]
    [TestCase(10, false, true, 4)]
    [TestCase(5, true, true, 3)]
    public void QualityLimitsVisiblePixels(int pixels, bool low, bool reducedMotion, int expected)
    {
        Assert.That(OrbitraBulletTrailPath.VisiblePixels(pixels, low, reducedMotion), Is.EqualTo(expected));
    }
}
