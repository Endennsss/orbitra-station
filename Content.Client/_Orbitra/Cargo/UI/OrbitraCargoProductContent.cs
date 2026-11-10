using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Cargo.UI;

public sealed class OrbitraCargoProductContent : BoxContainer
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var icon = GetChild(0);
        var name = GetChild(1);
        var price = GetChild(2);
        icon.Measure(availableSize);
        price.Measure(availableSize);

        // Резервируем цену до измерения многострочного названия.
        var fixedWidth = icon.DesiredSize.X + price.DesiredSize.X + 2 * SeparationOverride.GetValueOrDefault();
        name.Measure(new Vector2(Math.Max(0, availableSize.X - fixedWidth), availableSize.Y));
        var height = Math.Max(name.DesiredSize.Y, Math.Max(icon.DesiredSize.Y, price.DesiredSize.Y));
        return new Vector2(fixedWidth + name.DesiredSize.X, height);
    }
}
