using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Content.Shared._Orbitra.Roadmap;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Lobby.Roadmap;

/// <summary>Положение звезды и точки соединения вычисляются из порядка записей и ширины карты.</summary>
internal sealed class OrbitraAtlasRow : Control
{
    private static readonly float[] Lanes = [0.14f, 0.44f, 0.30f, 0.52f, 0.18f, 0.33f];
    private readonly Vector2[] _rays = new Vector2[10];
    private readonly int _index;
    private readonly int _count;
    private readonly AtlasStatus _status;
    internal readonly OrbitraContainerButton Button;
    internal bool Selected;

    internal OrbitraAtlasRow(AtlasEntry entry, int index, int count)
    {
        _index = index;
        _count = count;
        _status = entry.Content.Status;
        SetHeight = 86;
        MouseFilter = MouseFilterMode.Ignore;
        Button = new OrbitraContainerButton
        {
            StyleBoxOverride = new StyleBoxFlat(Color.Transparent),
            KeyboardFocusOnClick = true,
            ToolTip = entry.Content.Title,
        };
        var labels = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 1,
            Margin = new Thickness(44, 0, 4, 0),
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };
        labels.AddChild(new Label
        {
            Text = entry.Content.Title, ClipText = true, StyleClasses = { "OrbitraLobbyHeading" },
        });
        labels.AddChild(new Label
        {
            Text = OrbitraAtlasArt.StatusText(_status), ClipText = true,
            StyleClasses = { "OrbitraLobbyMuted" }, FontColorOverride = OrbitraAtlasArt.Tint(_status),
        });
        Button.AddChild(labels);
        AddChild(Button);
        OrbitraTooltips.Attach(Button);
    }

    private static float StarX(int index, float width) =>
        22 + Math.Max(0, width - 150) * Lanes[index % Lanes.Length];

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        Button.Measure(availableSize);
        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var left = StarX(_index, finalSize.X) - 22;
        Button.Arrange(UIBox2.FromDimensions(new Vector2(left, (finalSize.Y - 52) / 2),
            new Vector2(Math.Min(320, Math.Max(0, finalSize.X - left - 4)), 52)));
        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var x = StarX(_index, Width);
        var center = new Vector2(x * UIScale, PixelHeight / 2f);
        if (_index > 0)
            handle.DrawLine(new Vector2((StarX(_index - 1, Width) + x) / 2 * UIScale, 0), center, OrbitraAtlasArt.Route);
        if (_index + 1 < _count)
            handle.DrawLine(center, new Vector2((StarX(_index + 1, Width) + x) / 2 * UIScale, PixelHeight), OrbitraAtlasArt.Route);

        handle.DrawCircle(center, 14 * UIScale, OrbitraAtlasArt.Sky);
        if (Selected || Button.IsHovered || Button.HasKeyboardFocus())
        {
            var ink = Selected ? OrbitraAtlasArt.Amber : OrbitraAtlasArt.Mist;
            handle.DrawCircle(center, 16 * UIScale, ink.WithAlpha(0.10f));
            handle.DrawCircle(center, 16 * UIScale, ink.WithAlpha(0.78f), false);
            var tick = new Vector2(0, 19 * UIScale);
            handle.DrawLine(center - tick, center - tick * 1.18f, ink);
            handle.DrawLine(center + tick, center + tick * 1.18f, ink);
        }
        OrbitraAtlasArt.Star(handle, center, 8 * UIScale, _status, _rays);
    }
}
