using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Lobby;

/// <summary>Changes label placement using the width allocated to this row, not the window.</summary>
public sealed class OrbitraFormRow : BoxContainer
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var compact = availableSize.X < 560;
        Orientation = compact ? LayoutOrientation.Vertical : LayoutOrientation.Horizontal;
        SeparationOverride = 8;
        if (ChildCount > 0)
            GetChild(0).SetWidth = compact ? float.NaN : 176;
        return base.MeasureOverride(availableSize);
    }
}
