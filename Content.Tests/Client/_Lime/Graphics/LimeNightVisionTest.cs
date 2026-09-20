using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._Lime.NightVision;
using Content.Shared.Overlays;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Tests.Client._Lime.Graphics;

[TestFixture]
public sealed class LimeNightVisionTest
{
    [Test]
    public void ExposureHistoryResetsOnlyWhenSourceChanges()
    {
        var state = new LimeNightVisionPresentation();
        state.SetSource(new EntityUid(1), new EntityUid(2), Color.White, TimeSpan.Zero);
        var revision = state.Revision;
        state.SetSource(new EntityUid(1), new EntityUid(2), Color.White, TimeSpan.FromSeconds(1));
        Assert.That(state.Revision, Is.EqualTo(revision));
        state.SetSource(new EntityUid(3), new EntityUid(2), Color.White, TimeSpan.FromSeconds(2));
        Assert.That(state.Revision, Is.Not.EqualTo(revision));
        revision = state.Revision;
        state.Reset();
        state.SetSource(new EntityUid(3), new EntityUid(2), Color.White, TimeSpan.FromSeconds(3));
        Assert.That(state.Revision, Is.Not.EqualTo(revision));
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(144)]
    public void AdaptationIsFrameRateIndependentAndDarkRecoveryIsSlower(int fps)
    {
        var step = LimeNightVisionPresentation.GetAdaptationFactors(1f / fps);
        var residual = Vector2.One;
        for (var frame = 0; frame < fps; frame++)
            residual *= Vector2.One - step;

        var expected = Vector2.One - LimeNightVisionPresentation.GetAdaptationFactors(1f);
        Assert.That(residual.X, Is.EqualTo(expected.X).Within(0.00001f));
        Assert.That(residual.Y, Is.EqualTo(expected.Y).Within(0.00001f));
        Assert.That(residual.Y, Is.GreaterThan(residual.X));
        Assert.That(LimeNightVisionPresentation.GetAdaptationFactors(-1f), Is.EqualTo(Vector2.Zero));
    }

    [TestCase(1920, 1080)]
    [TestCase(3440, 1440)]
    [TestCase(801, 601)]
    [TestCase(600, 900)]
    public void WideVignetteTracksViewportAspectRatio(int width, int height)
    {
        var radii = LimeNightVisionPresentation.GetApertureRadii(new Vector2(width, height));
        Assert.That(radii.X / width, Is.EqualTo(0.62f).Within(0.0001f));
        Assert.That(radii.Y / height, Is.EqualTo(0.62f).Within(0.0001f));
        // Середины краёв видны через виньетку; углы полностью закрыты.
        Assert.That(width * .5f / radii.X, Is.LessThan(1f));
        Assert.That((new Vector2(width, height) * .5f / radii).Length(), Is.GreaterThan(1f));
    }

    [Test]
    public void ActivationRefreshDoesNotRestartAndDetachResets()
    {
        var state = new LimeNightVisionPresentation();
        state.SetSource(new EntityUid(1), new EntityUid(2), Color.White, TimeSpan.Zero);
        Assert.That(state.GetActivation(TimeSpan.Zero), Is.Zero);
        Assert.That(state.GetActivation(TimeSpan.FromSeconds(.1)), Is.EqualTo(.5f).Within(.001f));
        state.SetSource(new EntityUid(1), new EntityUid(2), Color.White, TimeSpan.FromSeconds(.1));
        Assert.That(state.GetActivation(TimeSpan.FromSeconds(.2)), Is.EqualTo(1));
        state.Reset();
        Assert.That(state.Source, Is.Null);
        Assert.That(state.GetActivation(TimeSpan.FromSeconds(1)), Is.Zero);
    }

    [Test]
    public void ChangingViewerOrSourceRestartsActivation()
    {
        var state = new LimeNightVisionPresentation();
        state.SetSource(new EntityUid(1), new EntityUid(2), Color.White, TimeSpan.Zero);
        state.SetSource(new EntityUid(3), new EntityUid(2), Color.White, TimeSpan.FromSeconds(1));
        Assert.That(state.GetActivation(TimeSpan.FromSeconds(1)), Is.Zero);
        state.SetSource(new EntityUid(3), new EntityUid(4), Color.White, TimeSpan.FromSeconds(2));
        Assert.That(state.GetActivation(TimeSpan.FromSeconds(2)), Is.Zero);
    }

    [Test]
    public void PrioritizedWearableWinsOverNaturalVision()
    {
        var viewer = new EntityUid(1);
        var natural = new NightVisionComponent { Prioritized = false };
        var device = new NightVisionComponent { RelayOverlay = true, Prioritized = true, NoiseAmount = 1, NoiseMultiplier = 2 };
        var sources = new List<Entity<NightVisionComponent>> { Source(viewer, natural), Source(new EntityUid(2), device) };
        Assert.That(LimeNightVisionPresentation.SelectSource(viewer, sources)?.Comp, Is.SameAs(device));
        device.Enabled = false;
        Assert.That(LimeNightVisionPresentation.SelectSource(viewer, sources)?.Comp, Is.SameAs(natural));
        natural.Enabled = false;
        Assert.That(LimeNightVisionPresentation.SelectSource(viewer, sources), Is.Null);
    }

    [Test]
    public void RelayAndNoiseRulesRemainUnchanged()
    {
        var viewer = new EntityUid(1);
        var wrongSelf = new NightVisionComponent { RelayOverlay = true };
        var wrongOther = new NightVisionComponent { RelayOverlay = false };
        var loud = new NightVisionComponent { RelayOverlay = true, Prioritized = false, NoiseAmount = 1, NoiseMultiplier = 2 };
        var quiet = new NightVisionComponent { RelayOverlay = true, Prioritized = false, NoiseAmount = .2f, NoiseMultiplier = 1 };
        var sources = new List<Entity<NightVisionComponent>>
        {
            Source(viewer, wrongSelf), Source(new EntityUid(2), wrongOther),
            Source(new EntityUid(3), loud), Source(new EntityUid(4), quiet),
        };
        Assert.That(LimeNightVisionPresentation.SelectSource(viewer, sources)?.Comp, Is.SameAs(quiet));
    }

    private static Entity<NightVisionComponent> Source(EntityUid owner, NightVisionComponent component)
    {
        // В чистом тесте нет EntityManager, поэтому задаём владельца явно.
#pragma warning disable CS0618
        component.Owner = owner;
#pragma warning restore CS0618
        return (owner, component);
    }
}
