using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Контурная иконка фиксированного размера из общего набора текстур.</summary>
public sealed class OrbitraIcon : TextureRect
{
    private string? _icon;

    public string Icon
    {
        get => _icon ?? "";
        set
        {
            if (_icon != null) RemoveStyleClass("OrbitraIcon-" + _icon);
            _icon = value;
            AddStyleClass("OrbitraIcon-" + value);
        }
    }

    public OrbitraIcon()
    {
        SetSize = new Vector2(OrbitraUiMetrics.IconSize);
        Stretch = StretchMode.KeepAspectCentered;
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalAlignment = HAlignment.Center;
        VerticalAlignment = VAlignment.Center;
    }
}
