using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Flows existing fixed-size entries into as many columns as the viewport permits.</summary>
public sealed class OrbitraFlowGrid : GridContainer
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        MaxGridWidth = Math.Max(1, availableSize.X);
        return base.MeasureOverride(availableSize);
    }
}
