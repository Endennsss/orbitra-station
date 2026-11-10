using System.Numerics;
using Content.Shared.Atmos.EntitySystems;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Orbitra.Atmos;

internal static class OrbitraGasField
{
    public const int Tiles = SharedGasTileOverlaySystem.ChunkSize;
    public const int FieldSize = Tiles + 2;
    public const int PixelsPerTile = 8;
    public const int TextureSize = Tiles * PixelsPerTile + 2;
    public const float FadeSeconds = 0.16f;

    public static Vector4 Mix(ReadOnlySpan<byte> opacity, ReadOnlySpan<Vector3> palette)
    {
        var color = Vector3.Zero;
        var weight = 0f;
        for (var i = 0; i < Math.Min(opacity.Length, palette.Length); i++)
        {
            color += palette[i] * opacity[i];
            weight += opacity[i];
        }

        if (weight == 0f)
            return Vector4.Zero;

        var density = Math.Min(1f, weight / 255f);
        return new Vector4(color * (density / weight), density);
    }

    public static void Rasterize(ReadOnlySpan<Vector4> field, Span<Rgba32> pixels)
    {
        for (var y = 0; y < TextureSize; y++)
        for (var x = 0; x < TextureSize; x++)
        {
            var sample = Sample(field, (x - 0.5f) / PixelsPerTile,
                (TextureSize - 1.5f - y) / PixelsPerTile);
            var pixel = new Rgba32();
            pixel.FromVector4(sample);
            pixels[y * TextureSize + x] = pixel;
        }
    }

    public static Vector4 Sample(ReadOnlySpan<Vector4> field, float x, float y)
    {
        var cellX = (int) MathF.Floor(x);
        var cellY = (int) MathF.Floor(y);
        var center = Cell(field, cellX, cellY);
        if (center.W == 0f)
            return Vector4.Zero;

        var dx = x - cellX - 0.5f;
        var dy = y - cellY - 0.5f;
        var sx = dx < 0 ? -1 : 1;
        var sy = dy < 0 ? -1 : 1;
        dx = MathF.Abs(dx);
        dy = MathF.Abs(dy);
        var horizontal = Cell(field, cellX + sx, cellY);
        var vertical = Cell(field, cellX, cellY + sy);
        var diagonal = horizontal.W > 0f || vertical.W > 0f
            ? Cell(field, cellX + sx, cellY + sy)
            : Vector4.Zero;

        // Последний полупиксель обнуляем, чтобы линейная фильтрация не протягивала газ в сухую клетку.
        var coverageX = horizontal.W == 0f ? EdgeCoverage(0.5f - dx) : 1f;
        var coverageY = vertical.W == 0f ? EdgeCoverage(0.5f - dy) : 1f;
        var lower = Vector4.Lerp(center, horizontal, dx);
        var upper = Vector4.Lerp(vertical, diagonal, dx);
        return Vector4.Lerp(lower, upper, dy) * (coverageX * coverageY);
    }

    public static void BlendInto(Span<Rgba32> previous, ReadOnlySpan<Rgba32> target, float progress)
    {
        for (var i = 0; i < previous.Length; i++)
            previous[i].FromVector4(Vector4.Lerp(previous[i].ToVector4(), target[i].ToVector4(), progress));
    }

    private static Vector4 Cell(ReadOnlySpan<Vector4> field, int x, int y)
    {
        x++;
        y++;
        return (uint) x < FieldSize && (uint) y < FieldSize ? field[y * FieldSize + x] : Vector4.Zero;
    }

    private static float EdgeCoverage(float distance)
    {
        const float clearance = 0.5f / PixelsPerTile;
        var t = Math.Clamp((distance - clearance) / (0.4f - clearance), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
