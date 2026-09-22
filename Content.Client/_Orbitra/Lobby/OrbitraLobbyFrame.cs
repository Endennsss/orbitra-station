using System.Numerics;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.Lobby;

/// <summary>Reserves a full-screen footer independently of the centered lobby body.</summary>
public sealed class OrbitraLobbyFrame : Control
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (ChildCount != 2)
            return base.MeasureOverride(availableSize);
        var footer = GetChild(1);
        footer.Measure(availableSize);
        GetChild(0).Measure(new Vector2(availableSize.X, Math.Max(0, availableSize.Y - footer.DesiredSize.Y)));
        return availableSize;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (ChildCount != 2)
            return base.ArrangeOverride(finalSize);
        var height = GetChild(1).DesiredSize.Y;
        GetChild(0).Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(finalSize.X, Math.Max(0, finalSize.Y - height))));
        GetChild(1).Arrange(UIBox2.FromDimensions(new Vector2(0, finalSize.Y - height), new Vector2(finalSize.X, height)));
        return finalSize;
    }
}
