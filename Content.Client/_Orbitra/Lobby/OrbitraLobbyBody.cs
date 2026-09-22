using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Lobby;

/// <summary>Centers the crew column independently of either side panel.</summary>
public sealed class OrbitraLobbyBody : BoxContainer
{
    public float SideWidth { get; set; } = 400;

    private float CenterWidth(float width) => Math.Min(640, Math.Max(0, width - (GetChild(0).Visible ? 2 * SideWidth + 32 : 0)));

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (ChildCount != 3)
            return base.MeasureOverride(availableSize);
        var height = 0f;
        for (var i = 0; i < 3; i++)
        {
            var child = GetChild(i);
            child.Measure(new Vector2(i == 1 ? CenterWidth(availableSize.X) : SideWidth, availableSize.Y));
            height = Math.Max(height, child.DesiredSize.Y);
        }
        return new Vector2(availableSize.X, height);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (ChildCount != 3)
            return base.ArrangeOverride(finalSize);
        var width = CenterWidth(finalSize.X);
        GetChild(0).Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(SideWidth, finalSize.Y)));
        GetChild(1).Arrange(UIBox2.FromDimensions(new Vector2((finalSize.X - width) / 2, 0), new Vector2(width, finalSize.Y)));
        GetChild(2).Arrange(UIBox2.FromDimensions(new Vector2(finalSize.X - SideWidth, 0), new Vector2(SideWidth, finalSize.Y)));
        return finalSize;
    }
}
