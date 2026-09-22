using System.Numerics;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Two columns on wide forms, a single column when controls need room for their labels.</summary>
public sealed class OrbitraResponsiveGrid : GridContainer
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        Columns = availableSize.X < OrbitraUiMetrics.FormBreakpoint ? 1 : 2;
        return base.MeasureOverride(availableSize);
    }
}
