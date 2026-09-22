using System;
using System.Numerics;
using Content.Client._Orbitra.Shaders.Bloom;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client._Orbitra.Graphics;

[TestFixture]
[TestOf(typeof(OrbitraWorldBloomOverlay))]
public sealed class OrbitraWorldBloomOverlayTest
{
    [Test]
    public void SourceReturnsToSameWorldPositionAfterEmissionMaskRoundTrip()
    {
        var sourcePosition = new Vector2(12.5f, -7.25f);
        var layerMatrix = Matrix3x2.CreateTranslation(0.1f, 0.2f);
        var spriteMatrix = Matrix3x2.CreateScale(1.25f);
        var worldMatrix = layerMatrix * spriteMatrix *
                          Matrix3Helpers.CreateTransform(sourcePosition, Angle.FromDegrees(15f));

        var firstWorldToTarget = Matrix3x2.CreateTranslation(-4f, 9f) * Matrix3x2.CreateScale(16f, -16f);
        var secondWorldToTarget = Matrix3x2.CreateTranslation(8f, -3f) * Matrix3x2.CreateScale(24f, -24f);

        Assert.That(Matrix3x2.Invert(firstWorldToTarget, out var firstTargetToWorld), Is.True);
        Assert.That(Matrix3x2.Invert(secondWorldToTarget, out var secondTargetToWorld), Is.True);

        var firstTarget = OrbitraWorldBloomOverlay.GetSourceToTargetMatrix(
            layerMatrix,
            spriteMatrix,
            sourcePosition,
            Angle.FromDegrees(15f),
            firstWorldToTarget);
        var secondTarget = OrbitraWorldBloomOverlay.GetSourceToTargetMatrix(
            layerMatrix,
            spriteMatrix,
            sourcePosition,
            Angle.FromDegrees(15f),
            secondWorldToTarget);
        var firstRoundTrip = Vector2.Transform(Vector2.Transform(Vector2.Zero, firstTarget), firstTargetToWorld);
        var secondRoundTrip = Vector2.Transform(Vector2.Transform(Vector2.Zero, secondTarget), secondTargetToWorld);
        var expected = Vector2.Transform(Vector2.Zero, worldMatrix);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstRoundTrip.X, Is.EqualTo(expected.X).Within(0.0001f));
            Assert.That(firstRoundTrip.Y, Is.EqualTo(expected.Y).Within(0.0001f));
            Assert.That(secondRoundTrip.X, Is.EqualTo(expected.X).Within(0.0001f));
            Assert.That(secondRoundTrip.Y, Is.EqualTo(expected.Y).Within(0.0001f));
        }
    }

    [TestCase(0f, 0f)]
    [TestCase(-1f, 0f)]
    [TestCase(float.NaN, 0f)]
    [TestCase(0.4f, 1.4f)]
    [TestCase(1f, 5f)]
    [TestCase(2f, 5f)]
    public void BloomStrengthUsesPerceptualCurve(float configured, float expected)
    {
        Assert.That(OrbitraWorldBloomOverlay.GetBloomStrength(configured), Is.EqualTo(expected).Within(0.0001f));
    }

    [TestCase((int) OrbitraBloomQuality.Low, 8f, 1.76f)]
    [TestCase((int) OrbitraBloomQuality.Medium, 8f, 1.76f)]
    [TestCase((int) OrbitraBloomQuality.High, 16f, 3.52f)]
    [TestCase((int) OrbitraBloomQuality.High, 1000f, 16f)]
    [TestCase((int) OrbitraBloomQuality.Low, 0.5f, 0.5f)]
    public void BlurRadiusFitsPaddedMask(int quality, float pixelsPerMeter, float expected)
    {
        var radius = OrbitraWorldBloomOverlay.CalculateBlurRadius((OrbitraBloomQuality) quality, pixelsPerMeter);
        Assert.That(radius, Is.EqualTo(expected).Within(0.0001f));
        Assert.That(radius, Is.LessThan(OrbitraWorldBloomOverlay.TargetPadding));
    }

    [TestCase(1920, 1080, 4, 0f)]
    [TestCase(3441, 1441, 4, 37f)]
    [TestCase(801, 601, 2, 90f)]
    public void PaddedMaskCornersCompositeToOriginalViewport(int width, int height, int divisor, float rotation)
    {
        var viewportSize = new Vector2(width, height);
        var contentSize = new Vector2(width / divisor, height / divisor);
        var camera = Matrix3x2.CreateTranslation(-17.2f, 23.4f) *
                     Matrix3x2.CreateRotation((float) Angle.FromDegrees(rotation)) *
                     Matrix3x2.CreateScale(48f, -48f) * Matrix3x2.CreateTranslation(viewportSize / 2f);
        var mask = OrbitraWorldBloomOverlay.GetWorldToTargetMatrix(camera, viewportSize, contentSize);
        Assert.That(Matrix3x2.Invert(mask, out var composite), Is.True);
        var padding = new Vector2(OrbitraWorldBloomOverlay.TargetPadding);
        var topLeft = Vector2.Transform(Vector2.Transform(padding, composite), camera);
        var bottomRight = Vector2.Transform(Vector2.Transform(contentSize + padding, composite), camera);
        Assert.That(Vector2.Distance(topLeft, Vector2.Zero), Is.LessThan(0.001f));
        Assert.That(Vector2.Distance(bottomRight, viewportSize), Is.LessThan(0.001f));
    }

    [Test]
    public void GridPixelSnapMatchesRoundedViewportPosition()
    {
        var gridPosition = new Vector2(3.137f, -4.421f);
        var viewPosition = new Vector2(-2.25f, 1.75f);
        var viewRotation = Angle.FromDegrees(17f);
        var viewScale = new Vector2(48f, -48f);
        var viewportSize = new Vector2(1920f, 1080f);

        var offset = OrbitraWorldBloomOverlay.CalculatePixelSnapOffset(
            gridPosition,
            viewPosition,
            viewRotation,
            viewScale,
            viewportSize);
        var snappedScreenPosition = viewRotation.RotateVec(gridPosition + offset - viewPosition) * viewScale +
                                    viewportSize / 2f;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snappedScreenPosition.X, Is.EqualTo(MathF.Round(snappedScreenPosition.X)).Within(0.001f));
            Assert.That(snappedScreenPosition.Y, Is.EqualTo(MathF.Round(snappedScreenPosition.Y)).Within(0.001f));
        }
    }
}
