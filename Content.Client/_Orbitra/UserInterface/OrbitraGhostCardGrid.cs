using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Equal-width ghost target cards, measured at their actual column width for text wrapping.</summary>
public sealed class OrbitraGhostCardGrid : GridContainer
{
    private const float PreferredCardWidth = 200;

    public OrbitraGhostCardGrid()
    {
        HSeparationOverride = OrbitraUiMetrics.Small;
        VSeparationOverride = OrbitraUiMetrics.Small;
        HorizontalExpand = true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsFinite(availableSize.X) ? Math.Max(1, availableSize.X) : PreferredCardWidth;
        var columns = Math.Clamp((int) ((width + OrbitraUiMetrics.Small) /
                                      (PreferredCardWidth + OrbitraUiMetrics.Small)), 1, 4);
        if (Columns != columns)
            Columns = columns;
        // Ширину ограничиваем до измерения текста, иначе длинное имя растягивает всю сетку.
        var cardWidth = Math.Max(1, (width - (columns - 1) * OrbitraUiMetrics.Small) / columns);
        foreach (var child in Children)
        {
            child.SetWidth = cardWidth;
            // Неполный ряд не растягивает занятые ячейки и не центрирует одиночную карточку.
            child.HorizontalExpand = false;
            child.HorizontalAlignment = HAlignment.Left;
        }
        return base.MeasureOverride(availableSize);
    }
}
