using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Lobby;

/// <summary>Stacks existing controls without rebuilding them when the available width is narrow.</summary>
public sealed class OrbitraAdaptiveRow : BoxContainer
{
    public float Breakpoint { get; set; } = 560;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        Orientation = availableSize.X < Breakpoint ? LayoutOrientation.Vertical : LayoutOrientation.Horizontal;
        return base.MeasureOverride(availableSize);
    }
}
