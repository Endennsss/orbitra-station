using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Lobby;

/// <summary>Two columns on wide forms, a single column when controls need room for their labels.</summary>
public sealed class OrbitraResponsiveGrid : GridContainer
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        Columns = availableSize.X < 560 ? 1 : 2;
        return base.MeasureOverride(availableSize);
    }
}
