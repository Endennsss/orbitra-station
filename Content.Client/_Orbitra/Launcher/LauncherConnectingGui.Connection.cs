namespace Content.Client.Launcher;

public sealed partial class LauncherConnectingGui
{
    private void UpdateDockingPage(LauncherConnecting.Page page)
    {
        ExitButton.Text = Loc.GetString(_state.CanReturnToMenu ? "orbitra-connecting-back" : "connecting-exit");
        if (page == LauncherConnecting.Page.Connecting)
        {
            _redial = false;
            _waitTime = 0;
            FailureScroll.VScroll = 0;
            DisconnectScroll.VScroll = 0;
            Docking.Reset();
            ConnectionStateChanged(_state.ConnectionState);
            return;
        }

        if (_state.WasDocked)
            Docking.StopAtDock();
        else
            Docking.Stop();

        HandleDisconnectReason(page == LauncherConnecting.Page.Disconnected ? _disconnectInfo.LastNetDisconnectedArgs : null);
    }

    private void UpdateRetryButton()
    {
        var failed = _state.CurrentPage == LauncherConnecting.Page.ConnectFailed;
        var button = failed ? RetryButton : ReconnectButton;
        button.Disabled = _waitTime > 0;
        if (button.Disabled)
        {
            button.Text = Loc.GetString("connecting-redial-wait", ("time", MathF.Ceiling(_waitTime)));
            return;
        }

        var label = _redial ? "connecting-redial" : failed ? "connecting-retry" : "connecting-reconnect";
        button.Text = Loc.GetString(label);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        _state.PageChanged += OnPageChanged;
        _state.ConnectFailReasonChanged += ConnectFailReasonChanged;
        _state.ConnectionStateChanged += ConnectionStateChanged;
        _state.ConnectFailed += HandleDisconnectReason;
        _disconnectInfo.LastNetDisconnectedArgsChanged += LastNetDisconnectedArgsChanged;

        ConnectFailReasonChanged(_state.ConnectFailReason);
        OnPageChanged(_state.CurrentPage);
    }

    protected override void ExitedTree()
    {
        _disconnectInfo.LastNetDisconnectedArgsChanged -= LastNetDisconnectedArgsChanged;
        _state.PageChanged -= OnPageChanged;
        _state.ConnectFailReasonChanged -= ConnectFailReasonChanged;
        _state.ConnectionStateChanged -= ConnectionStateChanged;
        _state.ConnectFailed -= HandleDisconnectReason;

        base.ExitedTree();
    }
}
