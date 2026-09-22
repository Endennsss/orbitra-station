using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Centers a full-width bounded form even when all field labels support clipping.</summary>
public sealed class OrbitraSettingsForm : BoxContainer
{
    public OrbitraSettingsForm()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalAlignment = HAlignment.Center;
        HorizontalExpand = true;
        MaxWidth = OrbitraUiMetrics.FormWidth;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var measured = base.MeasureOverride(availableSize);
        return new Vector2(float.IsFinite(availableSize.X) ? availableSize.X : measured.X, measured.Y);
    }
}
