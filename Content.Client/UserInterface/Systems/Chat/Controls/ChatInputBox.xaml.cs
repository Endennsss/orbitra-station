using Content.Shared.Chat;
using Content.Shared.Input;
using Content.Client.UserInterface.Systems.Chat;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

[Virtual]
public class ChatInputBox : PanelContainer
{
    public const string StyleClassChatPanel = "ChatPanel";
    public const string StyleClassChatLineEdit = "ChatLineEdit";
    public const string StyleClassChatFilterOptionButton = "ChatFilterOptionButton";

    public readonly ChannelSelectorButton ChannelSelector;
    // Orbitra added start - быстрый выбор отдела для голосовой рации.
    public readonly Content.Client._Orbitra.UserInterface.OrbitraRadioChannelButton RadioChannelSelector;
    // Orbitra added end
    public readonly HistoryLineEdit Input;
    public readonly ChannelFilterButton FilterButton;
    protected readonly BoxContainer Container;
    protected ChatChannel ActiveChannel { get; private set; } = ChatChannel.Local;

    public ChatInputBox()
    {
        Container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4
        };
        AddChild(Container);

        ChannelSelector = new ChannelSelectorButton
        {
            Name = "ChannelSelector",
            ToggleMode = true,
            StyleClasses = { ChannelSelectorItemButton.StyleClassChatSelectorOptionButton },
            MinWidth = 75
        };
        Container.AddChild(ChannelSelector);
        // Orbitra added start - кнопка канала находится рядом с обычным селектором чата.
        RadioChannelSelector = new Content.Client._Orbitra.UserInterface.OrbitraRadioChannelButton
        {
            Name = "RadioChannelSelector",
            MinWidth = 32,
            ToolTip = Loc.GetString("orbitra-voice-chat-radio-select")
        };
        Container.AddChild(RadioChannelSelector);
        // Orbitra added end
        Input = new HistoryLineEdit
        {
            Name = "Input",
            PlaceHolder = GetChatboxInfoPlaceholder(),
            HorizontalExpand = true,
            StyleClasses = { StyleClassChatLineEdit }
        };
        Container.AddChild(Input);
        FilterButton = new ChannelFilterButton
        {
            Name = "FilterButton",
            StyleClasses = { StyleClassChatFilterOptionButton }
        };
        Container.AddChild(FilterButton);
        AddStyleClass(StyleClassChatPanel);
        ChannelSelector.OnChannelSelect += UpdateActiveChannel;
        // Orbitra added start - выбор радио-канала синхронизирован с радио-PTT.
        RadioChannelSelector.OnChannelSelect += channel =>
        {
            ChannelSelector.Select(ChatSelectChannel.Radio);
            UserInterfaceManager.GetUIController<ChatUIController>().SetSelectedRadioChannel(channel);
        };
        // Orbitra added end
    }

    private void UpdateActiveChannel(ChatSelectChannel selectedChannel)
    {
        ActiveChannel = (ChatChannel) selectedChannel;
    }

    private static string GetChatboxInfoPlaceholder()
    {
        return (BoundKeyHelper.IsBound(ContentKeyFunctions.FocusChat),
                BoundKeyHelper.IsBound(ContentKeyFunctions.CycleChatChannelForward)) switch
            {
                (true, true) => Loc.GetString("hud-chatbox-info",
                    ("talk-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.FocusChat)),
                    ("cycle-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.CycleChatChannelForward))),
                (true, false) => Loc.GetString("hud-chatbox-info-talk",
                    ("talk-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.FocusChat))),
                (false, true) => Loc.GetString("hud-chatbox-info-cycle",
                    ("cycle-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.CycleChatChannelForward))),
                (false, false) => Loc.GetString("hud-chatbox-info-unbound")
            };
    }
}
