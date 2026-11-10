using System;
using System.Numerics;
using Content.Client._Orbitra.Atmos;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
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
    public void MissingChunksAndMapChangesDisposeCachedResources()
    {
        using var cache = new OrbitraGasCache<TrackedResource>();
        var removed = new TrackedResource();
        var retained = new TrackedResource();
        cache.ChangeMap(new MapId(1));
        cache.Entries.Add((new EntityUid(1), (0, 0)), removed);
        cache.Entries.Add((new EntityUid(1), (1, 0)), retained);
        cache.Retain(key => key.Index.X == 1);
        Assert.That(removed.Disposed, Is.True);
        Assert.That(retained.Disposed, Is.False);
        cache.ChangeMap(new MapId(2));
        Assert.That(retained.Disposed, Is.True);
        Assert.That(cache.Entries, Is.Empty);
    }

    private sealed class TrackedResource : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}
