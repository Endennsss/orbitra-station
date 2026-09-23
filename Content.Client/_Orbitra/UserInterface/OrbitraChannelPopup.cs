using System.Numerics;
using System.Linq;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Vertical, screen-bounded presentation of the existing channel selector.</summary>
internal static class OrbitraChannelPopup
{
    internal static UIBox2 Place(ChannelSelectorButton owner)
    {
        var popup = owner.Popup;
        if (!popup.HasStyleClass("OrbitraChannelPopup"))
        {
            popup.AddStyleClass("OrbitraChannelPopup");
            var rows = (BoxContainer) popup.GetChild(0);
            rows.Orphan();
            rows.Name = "OrbitraChannelRows";
            rows.Orientation = BoxContainer.LayoutOrientation.Vertical;
            rows.SeparationOverride = 4;
            var scroll = new ScrollContainer { HScrollEnabled = false };
            scroll.AddChild(rows);
            var panel = new PanelContainer { StyleClasses = { "OrbitraWindowSurface" }, Margin = new Thickness(0) };
            panel.AddChild(scroll);
            popup.AddChild(panel);
            OrbitraHudMenus.StyleScrollbars(scroll);
        }
        var list = popup.GetChild(0).GetChild(0).Children.OfType<BoxContainer>().Single();
        foreach (var child in list.Children)
        {
            if (child is not Button button)
                continue;
            button.MinHeight = 32;
            button.TextAlign = Label.AlignMode.Left;
            button.ClipText = false;
            button.Label.Margin = new Thickness(12, 2);
        }
        var screen = owner.Root?.Size ?? owner.UserInterfaceManager.RootControl.Size;
        var width = Math.Min(240, Math.Max(0, screen.X - 32));
        var above = Math.Max(0, owner.GlobalPosition.Y - 16);
        var below = Math.Max(0, screen.Y - owner.GlobalPosition.Y - owner.Height - 16);
        var wanted = Math.Min(292, list.ChildCount * 36 + 2);
        var up = below < wanted && above > below;
        var height = Math.Min(wanted, up ? above : below);
        popup.MinSize = Vector2.Zero;
        popup.SetSize = popup.MaxSize = new Vector2(width, height);
        var x = Math.Clamp(owner.GlobalPosition.X, 16, Math.Max(16, screen.X - width - 16));
        var y = up ? owner.GlobalPosition.Y - height : owner.GlobalPosition.Y + owner.Height;
        return UIBox2.FromDimensions(new Vector2(x, y), popup.SetSize);
    }
}
