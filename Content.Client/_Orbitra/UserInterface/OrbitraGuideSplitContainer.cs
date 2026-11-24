using System.Numerics;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Выбирает режим навигации по справочнику до измерения панелей.</summary>
public sealed class OrbitraGuideSplitContainer : SplitContainer
{
    public event Action<float>? AvailableWidthChanged;
    private float _lastWidth = -1;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (float.IsFinite(availableSize.X) && _lastWidth != availableSize.X)
        {
            _lastWidth = availableSize.X;
            AvailableWidthChanged?.Invoke(availableSize.X);
        }
        return base.MeasureOverride(availableSize);
    }
}
