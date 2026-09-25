using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using UsernameHelpers = Robust.Shared.AuthLib.UsernameHelpers;

namespace Content.Client.MainMenu;

[CVarDefs]
public static class OrbitraDevLobbyCVars
{
    public static readonly CVarDef<string> Channel = CVarDef.Create("orbitra.dev_lobby_channel", "", CVar.CLIENTONLY);
}

public sealed partial class MainScreen
{
    private ResPath _orbitraLobbyRequest = default;
    private ResPath _orbitraLobbyReply = default;
    private bool _orbitraLobbyPending;
    private float _orbitraLobbyElapsed;
    private float _orbitraLobbyPoll;

    private void InitializeOrbitraDevLobby()
    {
#if !FULL_RELEASE
        if (!Guid.TryParseExact(_configurationManager.GetCVar(OrbitraDevLobbyCVars.Channel), "N", out var channel))
            return;
        var directory = new ResPath("/_Orbitra/dev_lobby");
        _resourceCache.UserData.CreateDir(directory);
        _orbitraLobbyRequest = directory / (channel.ToString("N") + ".request");
        _orbitraLobbyReply = directory / (channel.ToString("N") + ".reply");
        _mainMenuControl.OrbitraDevLobbyButton.Visible = true;
        _mainMenuControl.OrbitraDevLobbyButton.OnPressed += OnOrbitraDevLobbyPressed;
#endif
    }

    private void ShutdownOrbitraDevLobby()
    {
        _mainMenuControl.OrbitraDevLobbyButton.OnPressed -= OnOrbitraDevLobbyPressed;
        _orbitraLobbyPending = false;
    }

    private void OnOrbitraDevLobbyPressed(BaseButton.ButtonEventArgs args)
    {
        if (_isConnecting || _orbitraLobbyPending)
            return;
        if (!UsernameHelpers.IsNameValid(_mainMenuControl.UsernameBox.Text.Trim(), out _))
        {
            _userInterfaceManager.Popup(Loc.GetString("main-menu-invalid-username"));
            return;
        }
        if (_resourceCache.UserData.Exists(_orbitraLobbyReply))
            _resourceCache.UserData.Delete(_orbitraLobbyReply);
        _resourceCache.UserData.WriteAllText(_orbitraLobbyRequest, "start");
        _orbitraLobbyPending = true;
        _orbitraLobbyElapsed = _orbitraLobbyPoll = 0;
        _setConnectingState(true);
        _mainMenuControl.OrbitraDevLobbyButton.Text = Loc.GetString("orbitra-dev-lobby-starting");
    }

    public override void FrameUpdate(FrameEventArgs e)
    {
        base.FrameUpdate(e);
        if (!_orbitraLobbyPending)
            return;
        _orbitraLobbyElapsed += e.DeltaSeconds;
        _orbitraLobbyPoll += e.DeltaSeconds;
        if (_orbitraLobbyPoll < 0.25f)
            return;
        _orbitraLobbyPoll = 0;
        if (!_resourceCache.UserData.TryReadAllText(_orbitraLobbyReply, out var result))
        {
            if (_orbitraLobbyElapsed < 305)
                return;
            result = "orbitra-dev-lobby-timeout";
        }
        _orbitraLobbyPending = false;
        _setConnectingState(false);
        _mainMenuControl.OrbitraDevLobbyButton.Text = Loc.GetString("orbitra-dev-lobby-connect");
        if (result == "ready")
            TryConnect("127.0.0.1:1214");
        else
            _userInterfaceManager.Popup(Loc.GetString(result is "orbitra-dev-lobby-port-busy" or
                "orbitra-dev-lobby-no-server" or "orbitra-dev-lobby-build-failed" or "orbitra-dev-lobby-timeout" ? result : "orbitra-dev-lobby-start-failed"));
    }
}
