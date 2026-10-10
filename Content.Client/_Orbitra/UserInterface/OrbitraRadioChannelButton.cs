using System;
using System.Numerics;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.Radio;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>
/// Компактный выбор отдела рядом с обычным селектором канала чата.
/// </summary>
public sealed class OrbitraRadioChannelButton : ChatPopupButton<OrbitraRadioChannelPopup>
{
    public event Action<RadioChannelPrototype>? OnChannelSelect;

    public OrbitraRadioChannelButton()
    {
        AddStyleClass("ChatSelectorOptionButton");
        Text = Loc.GetString("orbitra-voice-chat-radio-select-short");
        Popup.Selected += channel =>
        {
            Text = channel.LocalizedName;
            OnChannelSelect?.Invoke(channel);
        };
    }

    protected override UIBox2 GetPopupPosition()
    {
        return Popup.Place(this);
    }
}

public sealed class OrbitraRadioChannelPopup : Popup
{
    private readonly BoxContainer _channels;
    private readonly Content.Client.UserInterface.Systems.Chat.ChatUIController _controller;

    public event Action<RadioChannelPrototype>? Selected;

    public OrbitraRadioChannelPopup()
    {
        _controller = UserInterfaceManager.GetUIController<Content.Client.UserInterface.Systems.Chat.ChatUIController>();
        _channels = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2
        };
        var panel = new PanelContainer
        {
            StyleClasses = { "OrbitraWindowSurface" },
            Margin = new Thickness(0)
        };
        panel.AddChild(_channels);
        AddChild(panel);
        OnVisibilityChanged += _ =>
        {
            if (Visible)
                Rebuild();
        };
    }

    internal UIBox2 Place(OrbitraRadioChannelButton owner)
    {
        Rebuild();
        var screen = owner.Root?.Size ?? owner.UserInterfaceManager.RootControl.Size;
        var width = MathF.Min(240f, MathF.Max(180f, screen.X - 32f));
        var rowCount = Math.Max(1, _channels.ChildCount);
        var height = MathF.Min(292f, rowCount * 36f + 2f);
        MinSize = Vector2.Zero;
        SetSize = MaxSize = new Vector2(width, height);
        var x = Math.Clamp(owner.GlobalPosition.X, 16f, Math.Max(16f, screen.X - width - 16f));
        var below = Math.Max(0f, screen.Y - owner.GlobalPosition.Y - owner.Height - 16f);
        var above = Math.Max(0f, owner.GlobalPosition.Y - 16f);
        var y = below < height && above > below
            ? owner.GlobalPosition.Y - height
            : owner.GlobalPosition.Y + owner.Height;
        return UIBox2.FromDimensions(new Vector2(x, y), SetSize);
    }

    private void Rebuild()
    {
        _channels.RemoveAllChildren();
        var channels = _controller.GetRadioChannelOptions();
        if (channels.Count == 0)
        {
            _channels.AddChild(new Label { Text = Loc.GetString("orbitra-voice-chat-radio-unavailable") });
            return;
        }

        foreach (var option in channels)
        {
            var button = new Button
            {
                Text = option.Channel.LocalizedName,
                HorizontalExpand = true,
                Disabled = !option.Available,
                ToolTip = option.Available
                    ? option.Channel.ID
                    : Loc.GetString("orbitra-voice-chat-radio-locked")
            };
            button.AddStyleClass("OrbitraRadioChannelRow");
            button.OnPressed += _ =>
            {
                if (!option.Available)
                    return;

                Selected?.Invoke(option.Channel);
                Close();
            };
            _channels.AddChild(button);
        }
    }
}
