using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.Lobby;

/// <summary>A decorative screen-space grounding shadow, unrelated to world lighting.</summary>
public sealed class OrbitraLobbyShadow : Control
{
    private readonly Vector2[] _points = new Vector2[34];

    protected override void Draw(DrawingHandleScreen handle)
    {
        var points = _points;
        var center = PixelSize / 2f;
        points[0] = center;
        for (var i = 0; i <= 32; i++)
        {
            var angle = i * MathF.Tau / 32;
            points[i + 1] = center + new Vector2(MathF.Cos(angle) * PixelWidth * 0.32f,
                MathF.Sin(angle) * PixelHeight * 0.45f);
        }
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, points, Color.Black.WithAlpha(0.3f));
    }
}
