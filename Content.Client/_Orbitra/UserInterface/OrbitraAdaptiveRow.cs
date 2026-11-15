using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Переносит элементы в столбец, если их подписи не помещаются в одну строку.</summary>
public sealed class OrbitraAdaptiveRow : BoxContainer
{
    public float Breakpoint { get; set; } = OrbitraUiMetrics.FormBreakpoint;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var stack = availableSize.X < Breakpoint;
        if (!stack && float.IsFinite(availableSize.X))
        {
            var naturalSize = new Vector2(float.PositiveInfinity, availableSize.Y);
            var width = 0f;
            var visible = 0;
            foreach (var child in Children)
            {
                if (!child.Visible)
                    continue;

                child.Measure(naturalSize);
                width += child.DesiredSize.X;
                visible++;
            }

            var separation = SeparationOverride ?? StylePropertyDefault(StylePropertySeparation, 0);
            stack = width + separation * Math.Max(0, visible - 1) > availableSize.X;
        }

        var orientation = stack ? LayoutOrientation.Vertical : LayoutOrientation.Horizontal;
        if (Orientation != orientation)
            Orientation = orientation;
        return base.MeasureOverride(availableSize);
    }
}
