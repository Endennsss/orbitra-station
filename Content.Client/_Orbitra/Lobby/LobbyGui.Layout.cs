using Content.Client._Orbitra.Lobby;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.UserInterface; // Orbitra-Edit

namespace Content.Client.Lobby.UI;

public sealed partial class LobbyGui
{
    private float _orbitraChatWidth = 400;
    private bool _orbitraChatVisible = true;
    private bool _orbitraInfoOpen;
    private bool _orbitraChatOpen;
    private LobbyGuiState _orbitraState;
    private OrbitraVisibility _orbitraMoreMotion = default!;
    private OrbitraVisibility _orbitraInfoMotion = default!;
    private OrbitraVisibility _orbitraDockChatMotion = default!;

    private void InitializeOrbitraLayout()
    {
        CollapseButton.Text = "";
        CollapseButton.SetSize = new System.Numerics.Vector2(32);
        CollapseButton.AddChild(new OrbitraIcon { Icon = "minus" });
        OrbitraKeyboardNavigation.Attach(this);
        _orbitraMoreMotion = new OrbitraVisibility(MorePanel);
        _orbitraInfoMotion = new OrbitraVisibility(ServerInfo);
        _orbitraDockChatMotion = new OrbitraVisibility(RightSide);
        SetAnchorPreset(OrbitraScrim, LayoutPreset.Wide);
        // Сохраняем штатные кнопки ссылок и их подписки, меняем только ориентацию.
        foreach (var banner in new BoxContainer[] { LinkBanner, DevInfoBanner })
        {
            foreach (var child in banner.Children)
            {
                if (child is not BoxContainer links)
                    continue;
                links.Orientation = BoxContainer.LayoutOrientation.Vertical;
                links.SeparationOverride = 6;
                links.HorizontalExpand = true;
                foreach (var button in links.Children)
                {
                    button.AddStyleClass("OrbitraLobbyButton");
                    OrbitraTooltips.Attach(button);
                    button.HorizontalExpand = true;
                    button.HorizontalAlignment = HAlignment.Stretch;
                }
            }
        }
        CollapseButton.OnPressed += _ => SetOrbitraChatVisible(false);
        ChatToggle.OnPressed += _ =>
        {
            _orbitraChatVisible = true;
            _orbitraChatOpen = !_orbitraChatOpen;
            _orbitraInfoOpen = false;
            UpdateOrbitraLayout();
        };
        InfoToggle.OnPressed += _ =>
        {
            _orbitraInfoOpen = !_orbitraInfoOpen;
            _orbitraChatOpen = false;
            UpdateOrbitraLayout();
        };
        AboutButton.OnToggled += args => _orbitraInfoMotion.SetShown(args.Pressed);
        MoreButton.OnToggled += args => _orbitraMoreMotion.SetShown(args.Pressed);
        OnResized += UpdateOrbitraLayout;
        UpdateOrbitraLayout();
    }

    /// <summary>Applies the server's existing chat-width preference within usable bounds.</summary>
    public void SetOrbitraChatWidth(float width)
    {
        _orbitraChatWidth = float.IsFinite(width) ? Math.Clamp(width, 360, 480) : 400;
        UpdateOrbitraLayout();
    }

    /// <summary>Displays readiness independently of the action label.</summary>
    public void SetOrbitraReadyState(bool ready, bool started)
    {
        var changed = ReadyStatus.Visible != (ready && !started);
        ReadyStatus.Visible = ready && !started;
        if (changed && ready && !started)
            OrbitraMotion.Pulse(ReadyStatus);
        if (started)
            StartTime.Text = Loc.GetString("orbitra-lobby-round-running");
    }

    private void SetOrbitraChatVisible(bool visible)
    {
        _orbitraChatVisible = visible;
        _orbitraChatOpen = visible;
        UpdateOrbitraLayout();
    }

    private void SwitchOrbitraState(LobbyGuiState state)
    {
        var changed = _orbitraState != state;
        _orbitraState = state;
        DefaultState.Visible = state == LobbyGuiState.Default;
        DefaultMotion.Visible = state == LobbyGuiState.Default;
        CharacterSetupState.Visible = state == LobbyGuiState.CharacterSetup;
        _orbitraInfoOpen = _orbitraChatOpen = false;
        MoreButton.Pressed = false;
        _orbitraMoreMotion.SetShown(false, true);
        UpdateOrbitraLayout();
        if (state == LobbyGuiState.CharacterSetup)
        {
            CharacterPreview.FinishOrbitraTransition();
            UserInterfaceManager.GetUIController<LobbyUIController>().ReloadCharacterSetup();
        }
        if (changed)
        {
            var host = state == LobbyGuiState.Default ? DefaultMotion : CharacterSetupState;
            host.Reveal(OrbitraMotion.ScreenDuration, new System.Numerics.Vector2(0, 12));
        }
    }

    private void UpdateOrbitraLayout()
    {
        var width = Width;
        var editing = _orbitraState == LobbyGuiState.CharacterSetup;
        OrbitraLobbyHeader.Visible = !editing;
        var compact = Height < 850;
        var margin = editing ? 8 : width < 900 || compact ? 12 : 24;
        var bodyWidth = Math.Min(2200, width - margin * 2);
        var sideWidth = Math.Max(300, _orbitraChatWidth);
        var dockInfo = bodyWidth >= sideWidth * 2 + 560 + 32 && !editing;
        var dockChat = dockInfo;
        DefaultState.SideWidth = sideWidth;
        DefaultState.InvalidateMeasure();
        MoveOrbitraPanel(InfoPanel, dockInfo ? InfoDock : InfoDrawer);
        MoveOrbitraPanel(RightSide, dockChat ? ChatDock : ChatDrawer);
        InfoDock.Visible = dockInfo;
        ChatDock.Visible = dockChat;
        InfoDrawer.SetShown(!editing && !dockInfo && _orbitraInfoOpen, new System.Numerics.Vector2(-16, 0), editing || dockInfo);
        ChatDrawer.SetShown(!editing && !dockChat && _orbitraChatOpen, new System.Numerics.Vector2(16, 0), editing || dockChat);
        _orbitraDockChatMotion.SetShown(!editing && (dockChat ? _orbitraChatVisible : ChatDrawer.Visible), editing || !dockChat);
        RightSide.SetWidth = Math.Min(_orbitraChatWidth, Math.Max(280, width - 48));
        InfoToggle.Visible = !editing && !dockInfo;
        ChatToggle.Visible = !editing && (!dockChat || !_orbitraChatVisible);
        Brand.Visible = width >= 900;
        var previewWidth = Math.Min(640, bodyWidth - (dockInfo ? sideWidth * 2 + 32 : 0));
        ReadyCard.SetWidth = Math.Max(0, Math.Min(480, previewWidth));
        RoundActions.Orientation = ReadyCard.SetWidth < 400 ? BoxContainer.LayoutOrientation.Vertical : BoxContainer.LayoutOrientation.Horizontal;
        MainContainer.Margin = new Thickness(margin);
        MainContainer.SeparationOverride = compact ? 8 : 16;
        LeftSide.SeparationOverride = compact ? 8 : 16;
        Credits.Visible = !editing;
        MoveOrbitraPanel(OptionsButton, width < 900 ? MoreContents : OptionsHost);
        OptionsHost.Visible = width >= 900;
    }

    private static void MoveOrbitraPanel(Control panel, Control parent)
    {
        if (panel.Parent == parent)
            return;
        panel.Orphan();
        parent.AddChild(panel);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        UserInterfaceManager.DeferAction(() =>
        {
            if (Disposed || !VisibleInTree)
                return;
            OrbitraMotion.Reveal(MainContainer, OrbitraMotion.ScreenDuration);
            OrbitraMotion.Reveal(Credits, OrbitraMotion.ScreenDuration);
        });
    }
}
