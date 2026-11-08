// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Content-side adapter for the standard engine window frame.</summary>
internal static class OrbitraDefaultWindowChrome
{
    internal static void Apply(DefaultWindow window)
    {
        if (window.HasStyleClass("OrbitraWindowChrome"))
            return;
        var heading = Find<PanelContainer>(window, "WindowHeader");
        var title = Find<Label>(window, "TitleLabel");
        var oldClose = Find<TextureButton>(window, "CloseButton");
        if (heading == null || title == null || oldClose?.Parent == null)
            return;
        window.AddStyleClass("OrbitraWindowChrome");
        heading.StyleClasses.Clear();
        heading.AddStyleClass("OrbitraWindowHeader");
        // Высоту берём у штатной рамки: её область перетаскивания задана движком.
        var headerHeight = heading.MinHeight;
        heading.SetHeight = headerHeight;
        title.StyleIdentifier = null;
        title.StyleClasses.Clear();
        title.AddStyleClass("OrbitraWindowTitle");
        title.AddStyleClass("FancyWindowTitle");
        title.TooltipSupplier = _ => new OrbitraTooltip(title.Text ?? "");
        title.Margin = new Thickness(OrbitraUiMetrics.WindowPadding, 0, 0, 0);
        oldClose.Visible = false;
        var closeSize = new Vector2(Math.Max(0, Math.Min(OrbitraUiMetrics.CloseSize, headerHeight - 4)));
        var close = new OrbitraWindowCloseButton { SetSize = closeSize, MinSize = closeSize };
        close.OnPressed += _ => OrbitraEntryWindow.RequestClose(window);
        oldClose.Parent.AddChild(close);
        foreach (var child in window.Children)
            if (child is PanelContainer panel)
            {
                panel.StyleClasses.Clear();
                panel.AddStyleClass("OrbitraWindowSurface");
            }
        window.Contents.Margin = new Thickness(OrbitraUiMetrics.WindowPadding);
    }

    private static T? Find<T>(Control root, string name) where T : Control
    {
        foreach (var child in root.Children)
        {
            if (child.Name == name && child is T match)
                return match;
            if (Find<T>(child, name) is { } nested)
                return nested;
        }
        return null;
    }
}
