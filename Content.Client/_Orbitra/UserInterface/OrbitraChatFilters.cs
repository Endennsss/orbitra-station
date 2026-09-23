using System.Numerics;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Bounds and arranges the native filter controls without replacing their state or handlers.</summary>
internal static class OrbitraChatFilters
{
    internal static UIBox2 Place(ChannelFilterButton owner)
    {
        var popup = owner.Popup;
        var panel = popup.FindControl<PanelContainer>("FilterPopupPanel");
        var filters = popup.FindControl<BoxContainer>("FilterVBox");
        var highlights = popup.FindControl<BoxContainer>("HighlightsVBox");
        if (!popup.HasStyleClass("OrbitraChatFilters"))
        {
            popup.AddStyleClass("OrbitraChatFilters");
            var previous = filters.Parent!;
            filters.Orphan();
            highlights.Orphan();
            previous.Dispose();
            filters.MinWidth = 0;
            filters.HorizontalExpand = true;
            filters.Margin = new Thickness(0);
            filters.SeparationOverride = 4;
            highlights.MinWidth = 0;
            highlights.HorizontalExpand = true;
            highlights.Margin = new Thickness(0);
            var layout = new OrbitraAdaptiveRow { Breakpoint = 440, SeparationOverride = 16, Margin = new Thickness(12) };
            layout.AddChild(filters);
            layout.AddChild(highlights);
            var scroll = new ScrollContainer { HScrollEnabled = false };
            scroll.AddChild(layout);
            panel.AddChild(scroll);
            panel.RemoveStyleClass("BorderedWindowPanel");
            panel.AddStyleClass("OrbitraWindowSurface");
            OrbitraHudMenus.StyleScrollbars(scroll);
        }
        foreach (var child in filters.Children)
        {
            if (child is not CheckBox check)
                continue;
            check.ClipText = false;
            check.MinHeight = 32;
            check.HorizontalExpand = true;
        }
        var screen = owner.Root?.Size ?? owner.UserInterfaceManager.RootControl.Size;
        var width = Math.Min(560, Math.Max(0, screen.X - 32));
        var above = Math.Max(0, owner.GlobalPosition.Y - 16);
        var below = Math.Max(0, screen.Y - owner.GlobalPosition.Y - owner.Height - 16);
        var height = Math.Min(480, Math.Max(above, below));
        popup.MinSize = Vector2.Zero;
        popup.SetSize = popup.MaxSize = new Vector2(width, height);
        var x = Math.Clamp(owner.GlobalPosition.X + owner.Width - width, 16, Math.Max(16, screen.X - width - 16));
        var y = above >= below ? owner.GlobalPosition.Y - height : owner.GlobalPosition.Y + owner.Height;
        return UIBox2.FromDimensions(new Vector2(x, y), popup.SetSize);
    }
}
