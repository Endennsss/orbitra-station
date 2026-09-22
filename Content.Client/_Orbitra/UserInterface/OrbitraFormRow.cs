using System.Numerics;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Changes label placement using the width allocated to this row, not the window.</summary>
public sealed class OrbitraFormRow : BoxContainer
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var compact = availableSize.X < OrbitraUiMetrics.FormBreakpoint;
        Orientation = compact ? LayoutOrientation.Vertical : LayoutOrientation.Horizontal;
        SeparationOverride = OrbitraUiMetrics.Small;
        if (ChildCount > 0)
            GetChild(0).SetWidth = compact ? float.NaN : OrbitraUiMetrics.LabelWidth;
        return base.MeasureOverride(availableSize);
    }
}
