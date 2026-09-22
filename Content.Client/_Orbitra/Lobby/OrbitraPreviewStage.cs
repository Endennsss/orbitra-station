using System.Numerics;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.Lobby;

/// <summary>A fixed, clipped preview surface. Animation never changes its measured footprint.</summary>
public sealed class OrbitraPreviewStage : Control
{
    public float IncomingOffset { get; set; }
    public float OutgoingOffset { get; set; }

    public OrbitraPreviewStage()
    {
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        foreach (var child in Children)
            child.Measure(availableSize);
        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var previewIndex = 0;
        foreach (var child in Children)
        {
            if (child is OrbitraLobbyShadow)
            {
                child.Arrange(UIBox2.FromDimensions(new Vector2(0, finalSize.Y - 14), new Vector2(finalSize.X, 14)));
                continue;
            }
            child.Arrange(UIBox2.FromDimensions(new Vector2(previewIndex++ == 0 ? OutgoingOffset : IncomingOffset, 0), finalSize));
        }
        return finalSize;
    }

}
