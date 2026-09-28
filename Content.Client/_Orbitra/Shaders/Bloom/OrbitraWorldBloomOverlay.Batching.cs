using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;

namespace Content.Client._Orbitra.Shaders.Bloom;

internal sealed partial class OrbitraWorldBloomOverlay
{
    // Несколько независимых источников используют общие проходы, не смешивая свои маски перекрытия.
    private const int AtlasSourceSize = 1024;

    internal static Vector2i GetAtlasGrid(int sourceCount, Vector2i cellSize, int divisor)
    {
        var maxColumns = Math.Max(1, AtlasSourceSize / divisor / cellSize.X);
        var maxRows = Math.Max(1, AtlasSourceSize / divisor / cellSize.Y);
        var columns = Math.Clamp((int) MathF.Ceiling(MathF.Sqrt(sourceCount)), 1, maxColumns);
        var rows = Math.Clamp((sourceCount + columns - 1) / columns, 1, maxRows);
        return new Vector2i(columns, rows);
    }

    private void AddBatchEntry(int sourceIndex, Matrix3x2 matrix, Vector2i origin, Vector2i size)
    {
        if (!Matrix3x2.Invert(matrix, out var inverse))
            return;

        var region = UIBox2i.FromDimensions(origin, size);
        var worldBounds = inverse.TransformBox(Box2.FromDimensions(origin, size));
        _batch.Add(new BloomBatchEntry(sourceIndex, matrix, inverse, region, worldBounds));
    }

    private void DrawBatchLayers(IRenderHandle render, IEye eye, IClydeViewport viewport, int divisor, bool emissive)
    {
        var handle = render.DrawingHandleWorld;
        try
        {
            foreach (var entry in _batch)
            {
                // Большой перекрывающий спрайт не должен закрашивать соседнюю ячейку атласа.
                render.SetScissor(new UIBox2i(
                    entry.Region.Left * divisor, entry.Region.Top * divisor,
                    entry.Region.Right * divisor, entry.Region.Bottom * divisor));
                var matrix = entry.WorldToTarget * Matrix3x2.CreateScale(divisor);
                if (emissive)
                {
                    if (entry.SourceIndex < 0)
                        DrawTileEmission(handle, eye, viewport, matrix);
                    else
                        DrawSpriteLayers(handle, _orderedSprites[entry.SourceIndex], eye.Rotation, matrix, true);
                }

                DrawOccluders(handle, eye.Rotation, matrix, entry.WorldBounds, entry.SourceIndex);
            }
        }
        finally
        {
            render.SetScissor(null);
        }
    }

    private readonly record struct BloomBatchEntry(
        int SourceIndex,
        Matrix3x2 WorldToTarget,
        Matrix3x2 TargetToWorld,
        UIBox2i Region,
        Box2 WorldBounds);
}
