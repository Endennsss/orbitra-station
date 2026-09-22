using System.Numerics;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Stacks existing controls without rebuilding them when the available width is narrow.</summary>
public sealed class OrbitraAdaptiveRow : BoxContainer
{
    public float Breakpoint { get; set; } = OrbitraUiMetrics.FormBreakpoint;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        Orientation = availableSize.X < Breakpoint ? LayoutOrientation.Vertical : LayoutOrientation.Horizontal;
        return base.MeasureOverride(availableSize);
    }
}
