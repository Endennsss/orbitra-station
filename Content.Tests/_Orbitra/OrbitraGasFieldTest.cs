using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._Orbitra.Atmos;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraGasFieldTest
{
    [Test]
    public void GasMixturesUseOpacityAsColorWeightAndCapDensity()
    {
        var palette = new[] { Vector3.UnitX, Vector3.UnitZ };
        var sample = OrbitraGasField.Mix(new byte[] { 255, 85 }, palette);
        Assert.That(sample, Is.EqualTo(new Vector4(0.75f, 0f, 0.25f, 1f)));
    }

    [Test]
    public void DryTilesAndDiagonalCornersStayClear()
    {
        var tiles = new Vector4[OrbitraGasField.FieldSize * OrbitraGasField.FieldSize];
        var gas = new Vector4(0.4f, 0, 0, 0.4f);
        tiles[1 + OrbitraGasField.FieldSize] = gas;
        tiles[2 + 2 * OrbitraGasField.FieldSize] = gas;

        Assert.That(OrbitraGasField.Sample(tiles, 0.5f, 0.5f), Is.EqualTo(gas));
        Assert.That(OrbitraGasField.Sample(tiles, 1.05f, 0.95f), Is.EqualTo(Vector4.Zero));
        Assert.That(OrbitraGasField.Sample(tiles, 0.99f, 0.99f), Is.EqualTo(Vector4.Zero));
    }

    [Test]
    public void AdjacentChunksProduceTheSameSamplesAcrossTheirSharedEdge()
    {
        var left = new Vector4[OrbitraGasField.FieldSize * OrbitraGasField.FieldSize];
        var right = new Vector4[left.Length];
        var red = new Vector4(0.4f, 0, 0, 0.4f);
        var blue = new Vector4(0, 0, 0.2f, 0.2f);
        for (var y = 0; y < OrbitraGasField.FieldSize; y++)
        {
            left[y * OrbitraGasField.FieldSize + 8] = red;
            left[y * OrbitraGasField.FieldSize + 9] = blue;
            right[y * OrbitraGasField.FieldSize] = red;
            right[y * OrbitraGasField.FieldSize + 1] = blue;
        }

        Assert.That(OrbitraGasField.Sample(left, 8.0625f, 2.5f),
            Is.EqualTo(OrbitraGasField.Sample(right, 0.0625f, 2.5f)));
        Assert.That(OrbitraGasField.Sample(left, 7.9375f, 2.5f),
            Is.EqualTo(OrbitraGasField.Sample(right, -0.0625f, 2.5f)));
    }

    [Test]
    public void RetargetingPreservesTheCurrentOpacityInBothDirections()
    {
        var previous = new[] { new Rgba32(0, 0, 0, 0) };
        var target = new[] { new Rgba32(100, 0, 0, 100) };
        OrbitraGasField.BlendInto(previous, target, 0.5f);
        Assert.That(previous[0].A, Is.EqualTo(50));
        target[0] = default;
        OrbitraGasField.BlendInto(previous, target, 0.5f);
        Assert.That(previous[0].A, Is.EqualTo(25));
    }

    [Test]
    public void CamerasShareCacheUntilChunksLeaveNetworkVisibility()
    {
        using var cache = new OrbitraGasCache<TrackedResource>();
        var removed = new TrackedResource();
        var retained = new TrackedResource();
        var first = (new EntityUid(1), new Vector2i(0, 0));
        var second = (new EntityUid(2), new Vector2i(1, 0));
        cache.Reserve([first]);
        cache.Add(first, removed);
        cache.Reserve([second]);
        cache.Add(second, retained);
        cache.Reserve([first]);
        Assert.That(cache.TryGet(first, out var cached), Is.True);
        Assert.That(cached, Is.SameAs(removed));
        Assert.That(retained.Disposed, Is.False);
        cache.Retain(key => key.Index.X == 1);
        Assert.That(removed.Disposed, Is.True);
        Assert.That(retained.Disposed, Is.False);
        Assert.That(cache.TryGet(first, out _), Is.False);
    }

    [Test]
    public void CacheEvictsOldestChunkOutsideCurrentView()
    {
        using var cache = new OrbitraGasCache<TrackedResource>();
        var grid = new EntityUid(1);
        var oldest = new TrackedResource();
        var stillVisible = new TrackedResource();
        cache.Add((grid, new Vector2i(0, 0)), stillVisible);
        cache.Add((grid, new Vector2i(1, 0)), oldest);
        for (var i = 2; i < OrbitraGasCache<TrackedResource>.Capacity; i++)
            cache.Add((grid, new Vector2i(i, 0)), new TrackedResource());

        cache.Reserve([(grid, new Vector2i(0, 0)), (grid, new Vector2i(-1, 0))]);
        Assert.That(oldest.Disposed, Is.True);
        Assert.That(stillVisible.Disposed, Is.False);
    }

    [Test]
    public void SecondCameraFallsBackWhenCombinedViewsExceedBudget()
    {
        using var cache = new OrbitraGasCache<TrackedResource>();
        var grid = new EntityUid(1);
        var visible = new HashSet<(EntityUid Grid, Vector2i Index)>();
        for (var i = 0; i < OrbitraGasCache<TrackedResource>.Capacity; i++)
            visible.Add((grid, new Vector2i(i, 0)));
        Assert.That(cache.Reserve(visible), Is.True);
        var first = new TrackedResource();
        cache.Add((grid, new Vector2i(0, 0)), first);

        Assert.That(cache.Reserve([(grid, new Vector2i(-1, 0))]), Is.False);
        Assert.That(first.Disposed, Is.False);
        cache.BeginFrame();
        Assert.That(cache.Reserve([(grid, new Vector2i(-1, 0))]), Is.True);
    }

    private sealed class TrackedResource : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}
