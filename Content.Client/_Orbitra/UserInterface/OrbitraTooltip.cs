using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Scoped native tooltip surface. Specialized supplier controls remain intact inside it.</summary>
internal sealed class OrbitraTooltip : PanelContainer
{
    internal Control? Owner;
    internal Control? SuppliedContent;
    private bool _placed;
    private Vector2 _screen;
    private Vector2 _anchor;

    public OrbitraTooltip(string text) : this(new RichTextLabel())
    {
        // Обычная подсказка — текст, а не разметка; пустой хвост не создаёт лишние строки.
        ((RichTextLabel) GetChild(0)).SetMessage(text.TrimEnd('\r', '\n'));
    }

    internal OrbitraTooltip(Control content)
    {
        // Штатная оболочка supplier не должна рисовать вторую рамку внутри подсказки Orbitra.
        if (content is Robust.Client.UserInterface.CustomControls.Tooltip)
            content.AddStyleClass("OrbitraTooltipContent");
        MaxWidth = 360;
        MouseFilter = MouseFilterMode.Ignore;
        AddStyleClass("OrbitraTooltip");
        AddChild(content);
    }

    internal void Place(bool keyboard)
    {
        if (Root == null || Owner == null) return;
        var anchor = keyboard ? Owner.GlobalPosition + new Vector2(0, Owner.Height)
            : UserInterfaceManager.MousePositionScaled.Position;
        if (_placed && _screen == Root.Size && ((!keyboard && !Owner.TrackingTooltip) || _anchor == anchor))
            return;
        _placed = true;
        _screen = Root.Size;
        _anchor = anchor;
        var margin = new Vector2(OrbitraUiMetrics.ScreenMargin);
        MaxWidth = Math.Min(360, Math.Max(0, Root.Width - margin.X * 2));
        Measure(new Vector2(MaxWidth, Root.Height));
        var point = keyboard ? anchor + new Vector2(0, DesiredSize.Y + 4) : anchor;
        var position = new Vector2(point.X, point.Y - DesiredSize.Y);
        LayoutContainer.SetPosition(this, Vector2.Clamp(position, margin,
            Vector2.Max(margin, Root.Size - DesiredSize - margin)));
    }
}
