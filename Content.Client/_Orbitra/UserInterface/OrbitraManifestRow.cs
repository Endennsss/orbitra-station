using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Shared four-column geometry for manifest headers and wrapping player rows.</summary>
public sealed class OrbitraManifestRow : Control
{
    private const float Gap = 8;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = Math.Max(0, (availableSize.X - Gap * 3) / 4);
        var height = 0f;
        foreach (var child in Children)
        {
            child.Measure(new Vector2(width, availableSize.Y));
            height = Math.Max(height, child.DesiredSize.Y);
        }
        return new Vector2(availableSize.X, height);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var width = Math.Max(0, (finalSize.X - Gap * 3) / 4);
        var index = 0;
        foreach (var child in Children)
            child.Arrange(UIBox2.FromDimensions(new Vector2(index++ * (width + Gap), 0), new Vector2(width, finalSize.Y)));
        return finalSize;
    }
}
