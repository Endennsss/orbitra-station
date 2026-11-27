using System.Numerics;
using Content.Client._Orbitra.Stylesheets;
using Content.Shared._Orbitra.Roadmap;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.Lobby.Roadmap;

/// <summary>Условные знаки звёздной карты. Силуэт сохраняет значение статуса в монохромном режиме.</summary>
internal static class OrbitraAtlasArt
{
    internal static readonly Color Sky = Color.FromHex("#15191C");
    internal static readonly Color Grid = Color.FromHex("#252D31");
    internal static readonly Color Route = Color.FromHex("#736652");
    internal static readonly Color Amber = Color.FromHex("#E2BA78");
    internal static readonly Color Mist = Color.FromHex("#AAB8C1");

    internal static string StatusText(AtlasStatus status) => Loc.GetString(status switch
    {
        AtlasStatus.Active => "orbitra-atlas-status-active",
        AtlasStatus.Done => "orbitra-atlas-status-done",
        _ => "orbitra-atlas-status-planned",
    });

    internal static Color Tint(AtlasStatus status) => status switch
    {
        AtlasStatus.Active => Amber,
        AtlasStatus.Done => OrbitraPalettes.Highlight.Text,
        _ => Mist,
    };

    internal static void Star(DrawingHandleScreen handle, Vector2 center, float radius, AtlasStatus status, Vector2[] rays)
    {
        var color = Tint(status);
        var x = new Vector2(radius, 0);
        var y = new Vector2(0, radius);
        var a = (x + y) * 0.34f;
        var b = (x - y) * 0.34f;
        rays[0] = center;
        rays[1] = rays[9] = center + x;
        rays[2] = center + a;
        rays[3] = center + y;
        rays[4] = center - b;
        rays[5] = center - x;
        rays[6] = center - a;
        rays[7] = center - y;
        rays[8] = center + b;
        if (status == AtlasStatus.Planned)
        {
            handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, rays.AsSpan(1), color);
            return;
        }

        // Контур, кольцо и отметка выпуска читаются и при отключённом цвете.
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, rays, color);
        if (status == AtlasStatus.Active)
            handle.DrawCircle(center, radius * 1.35f, color, false);
        else
        {
            var corner = center + x * 0.78f + y * 0.85f;
            handle.DrawLine(corner - x * 0.45f - y * 0.38f, corner, color);
            handle.DrawLine(corner, corner + x * 0.67f - y * 0.85f, color);
        }
    }
}

/// <summary>Неподвижная координатная сетка и звёзды в пределах видимой части карты.</summary>
public sealed class OrbitraAtlasSky : Control
{
    private static readonly Vector2[] Stars = MakeField();

    public OrbitraAtlasSky()
    {
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    private static Vector2[] MakeField()
    {
        var random = new System.Random(4817);
        var stars = new Vector2[108];
        for (var i = 0; i < stars.Length; i++)
            stars[i] = new Vector2(random.NextSingle(), random.NextSingle());
        return stars;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(PixelSizeBox, OrbitraAtlasArt.Sky);
        var step = 72 * UIScale;
        for (var x = step / 2; x < PixelWidth; x += step)
            handle.DrawLine(new Vector2(x, 0), new Vector2(x, PixelHeight), OrbitraAtlasArt.Grid);
        for (var y = step / 2; y < PixelHeight; y += step)
            handle.DrawLine(new Vector2(0, y), new Vector2(PixelWidth, y), OrbitraAtlasArt.Grid);

        var pole = new Vector2(PixelWidth * 0.83f, PixelHeight * 0.36f);
        var radius = Math.Min(PixelWidth, PixelHeight) * 0.38f;
        handle.DrawCircle(pole, radius, OrbitraAtlasArt.Grid, false);
        handle.DrawCircle(pole, radius * 1.65f, OrbitraAtlasArt.Grid, false);
        handle.DrawLine(pole - Vector2.UnitX * 5 * UIScale, pole + Vector2.UnitX * 5 * UIScale, OrbitraAtlasArt.Route);
        handle.DrawLine(pole - Vector2.UnitY * 5 * UIScale, pole + Vector2.UnitY * 5 * UIScale, OrbitraAtlasArt.Route);

        for (var i = 0; i < Stars.Length; i++)
        {
            var position = Stars[i] * (Vector2) PixelSize;
            var size = i % 7 == 0 ? 1.6f : 1;
            var color = OrbitraAtlasArt.Mist.WithAlpha(i % 7 == 0 ? 0.65f : 0.34f);
            handle.DrawRect(UIBox2.FromDimensions(position, new Vector2(size * UIScale)), color);
        }
        handle.DrawRect(PixelSizeBox, OrbitraPalettes.PanelBorder, false);
    }
}

public sealed class OrbitraAtlasMarker : Control
{
    private readonly Vector2[] _rays = new Vector2[10];
    public AtlasStatus Status { get; set; }

    public OrbitraAtlasMarker() => MouseFilter = MouseFilterMode.Ignore;

    protected override void Draw(DrawingHandleScreen handle) =>
        OrbitraAtlasArt.Star(handle, PixelSize / 2f, Math.Min(PixelWidth, PixelHeight) * 0.36f, Status, _rays);
}
