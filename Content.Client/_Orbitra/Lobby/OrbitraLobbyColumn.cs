using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Lobby;

/// <summary>Reserves the round actions before measuring the flexible character preview.</summary>
public sealed class OrbitraLobbyColumn : BoxContainer
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (ChildCount != 2)
            return base.MeasureOverride(availableSize);
        var preview = GetChild(0);
        var footer = GetChild(1);
        footer.Measure(availableSize);
        preview.Measure(new Vector2(availableSize.X, Math.Max(0, availableSize.Y - footer.DesiredSize.Y - (SeparationOverride ?? 8))));
        return new Vector2(Math.Max(preview.DesiredSize.X, footer.DesiredSize.X),
            preview.DesiredSize.Y + footer.DesiredSize.Y + (SeparationOverride ?? 8));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (ChildCount != 2)
            return base.ArrangeOverride(finalSize);
        var footer = GetChild(1);
        var height = footer.DesiredSize.Y;
        var previewHeight = Math.Max(0, finalSize.Y - height - (SeparationOverride ?? 8));
        GetChild(0).Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(finalSize.X, previewHeight)));
        footer.Arrange(UIBox2.FromDimensions(new Vector2(0, finalSize.Y - height), new Vector2(finalSize.X, height)));
        return finalSize;
    }
}
