using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public partial class ChatBox
{
    /// <summary>Styles only chat chrome, leaving message formatting and opacity management native.</summary>
    private void InitializeOrbitra()
    {
        var column = (BoxContainer) Contents.Parent!;
        Contents.Orphan();
        var history = new PanelContainer { Name = "OrbitraChatHistory", VerticalExpand = true, Margin = new Thickness(8, 4), StyleClasses = { "OrbitraChatFrame" } };
        history.AddChild(Contents);
        column.AddChild(history);
        history.SetPositionInParent(0);
        ChatWindowPanel.AddStyleClass("OrbitraChatSurface");
        ChatInput.AddStyleClass("OrbitraChatInput");
        ChatInput.Margin = new Thickness(8, 4, 8, 8);
        ChatInput.Input.AddStyleClass("OrbitraChatInput");
        ChatInput.ChannelSelector.AddStyleClass("OrbitraChatButton");
        ChatInput.FilterButton.AddStyleClass("OrbitraChatButton");
        ChatInput.FilterButton.SetSize = new Vector2(32);
        ChatInput.ChannelSelector.MinHeight = 32;
        ChatInput.ChannelSelector.Modulate = Color.White;
        OrbitraEditorStyles.Apply(ChatInput.FilterButton.Popup);
        ChatInput.FilterButton.Popup.FindControl<PanelContainer>("FilterPopupPanel").AddStyleClass("OrbitraWindowSurface");
        foreach (var child in ChatInput.FilterButton.Children)
        {
            if (child is not TextureRect texture)
                continue;
            texture.Texture = null;
            texture.SetSize = new Vector2(16);
            texture.Stretch = TextureRect.StretchMode.KeepAspectCentered;
            texture.AddStyleClass("OrbitraIcon-list_filter");
        }
        OrbitraHudMenus.StyleScrollbars(Contents);
        OrbitraFocusRing.Attach(ChatInput.Input);
        OrbitraFocusRing.Attach(ChatInput.ChannelSelector);
        OrbitraFocusRing.Attach(ChatInput.FilterButton);
    }

    /// <summary>Adds shortcuts only to the explicitly connected separated-chat HUD.</summary>
    public void AddOrbitraQuickEmotes()
    {
        var column = (BoxContainer) ChatInput.Parent!;
        column.AddChild(new OrbitraQuickEmotes { Margin = new Thickness(8, 4) });
        column.GetChild(column.ChildCount - 1).SetPositionInParent(0);
    }
}
