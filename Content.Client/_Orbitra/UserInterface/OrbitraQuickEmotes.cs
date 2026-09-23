using System.Numerics;
using System.Linq;
using Content.Client.UserInterface.Systems.Emotes;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Chat shortcuts using native emote prototypes and the radial menu controller.</summary>
public sealed class OrbitraQuickEmotes : PanelContainer
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    private readonly OrbitraFlowGrid _buttons = new() { HSeparationOverride = 4, VSeparationOverride = 4 };
    private readonly List<string> _ids = new();

    public OrbitraQuickEmotes()
    {
        IoCManager.InjectDependencies(this);
        AddStyleClass("OrbitraChatFrame");
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, Margin = new Thickness(8) };
        column.AddChild(new Label { Text = _loc.GetString("orbitra-chat-quick-emotes"), StyleClasses = { "OrbitraLobbyMuted" } });
        var scroll = new ScrollContainer { HScrollEnabled = false, MaxHeight = 100, MinHeight = 44 };
        scroll.AddChild(_buttons);
        column.AddChild(scroll);
        AddChild(column);
        OrbitraHudMenus.StyleScrollbars(scroll);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _player.LocalPlayerAttached += OnPlayerChanged;
        _player.LocalPlayerDetached += OnPlayerChanged;
        Refresh();
    }

    protected override void ExitedTree()
    {
        _player.LocalPlayerAttached -= OnPlayerChanged;
        _player.LocalPlayerDetached -= OnPlayerChanged;
        base.ExitedTree();
    }

    private void OnPlayerChanged(EntityUid _) => Refresh();

    protected override void MouseEntered()
    {
        base.MouseEntered();
        Refresh();
    }

    private void Refresh()
    {
        var controller = UserInterfaceManager.GetUIController<EmotesUIController>();
        var emotes = controller.GetAvailableEmotes().OrderBy(e => e.Category).ThenBy(e => e.ID, StringComparer.Ordinal).ToArray();
        Visible = emotes.Length != 0;
        if (_ids.SequenceEqual(emotes.Select(e => e.ID)))
            return;
        foreach (var child in _buttons.Children.ToArray())
            child.Dispose();
        _ids.Clear();
        foreach (var emote in emotes)
        {
            var button = new ContainerButton
            {
                Name = emote.ID,
                SetSize = new Vector2(44),
                ToolTip = _loc.GetString(emote.Name),
                StyleClasses = { "OrbitraEmoteButton" },
            };
            button.AddChild(new TextureRect
            {
                Texture = _entities.System<SpriteSystem>().Frame0(emote.Icon),
                SetSize = new Vector2(28),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MouseFilter = MouseFilterMode.Ignore,
            });
            button.OnPressed += _ => { controller.TryPlayQuickEmote(emote); Refresh(); };
            OrbitraFocusRing.Attach(button);
            OrbitraTooltips.Attach(button);
            _buttons.AddChild(button);
            _ids.Add(emote.ID);
        }
    }
}
