using System.Numerics;
using Content.Client.UserInterface.Controls;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Explicit presentation hooks for HUD navigation and context-menu service icons.</summary>
internal static class OrbitraHudMenus
{
    internal static void StyleButton(MenuButton button, string icon)
    {
        button.SetOnlyStyleClass("OrbitraHudButton");
        button.Icon = null;
        foreach (var child in button.ButtonRoot.Children)
        {
            if (child is not TextureRect texture)
                continue;
            texture.AddStyleClass("OrbitraIcon-" + icon);
            texture.SetSize = new Vector2(24);
            texture.TextureScale = Vector2.One;
            texture.Stretch = TextureRect.StretchMode.KeepAspectCentered;
        }
        OrbitraFocusRing.Attach(button);
        OrbitraTooltips.Attach(button);
    }

    internal static TextureRect VerbIcon(SpriteSpecifier? sprite, SpriteSystem sprites)
    {
        var icon = (sprite as SpriteSpecifier.Texture)?.TexturePath.ToString() switch
        {
            "/Textures/Interface/character.svg.192dpi.png" => "user_round",
            "/Textures/Interface/VerbIcons/vv.svg.192dpi.png" => "eye",
            "/Textures/Interface/VerbIcons/examine.svg.192dpi.png" => "search",
            "/Textures/Interface/VerbIcons/debug.svg.192dpi.png" => "bug",
            "/Textures/Interface/VerbIcons/point.svg.192dpi.png" => "navigation",
            "/Textures/Interface/VerbIcons/smite.svg.192dpi.png" => "zap",
            "/Textures/Interface/AdminActions/tricks.png" => "wand_sparkles",
            "/Textures/Interface/gavel.svg.192dpi.png" => "gavel",
            "/Textures/Interface/VerbIcons/information.svg.192dpi.png" => "info",
            "/Textures/Interface/VerbIcons/refresh.svg.192dpi.png" => "refresh_cw",
            "/Textures/Interface/VerbIcons/delete.svg.192dpi.png" => "trash",
            "/Textures/Interface/VerbIcons/delete_transparent.svg.192dpi.png" => "trash",
            _ => null,
        };
        if (icon != null)
            return new OrbitraIcon { Icon = icon, SetSize = new Vector2(20) };

        // Неизвестные значки и игровые спрайты сохраняют штатный размер и источник.
        return new TextureRect
        {
            Texture = sprite == null ? null : sprites.Frame0(sprite),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
        };
    }

    internal static void StyleScrollbars(Control owner)
    {
        foreach (var child in owner.Children)
        {
            if (child is ScrollBar)
                child.AddStyleClass("OrbitraEditorControl");
            else
                StyleScrollbars(child);
        }
    }
}
