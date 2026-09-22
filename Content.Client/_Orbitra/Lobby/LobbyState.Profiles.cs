using Content.Client._Orbitra.Lobby;

namespace Content.Client.Lobby;

public sealed partial class LobbyState
{
    // Переключаем реальный слот только после снятия готовности, без отдельного сетевого протокола.
    private void OnOrbitraProfileRequested(int step)
    {
        var preferences = IoCManager.Resolve<IClientPreferencesManager>();
        if (!preferences.ServerDataLoaded || preferences.Preferences is not { } profiles)
            return;
        var slot = OrbitraLobbyPolicy.NextSlot(profiles.Characters.Keys, profiles.SelectedCharacterIndex, step);
        if (slot == null)
            return;
        SetReady(false);
        preferences.SelectCharacter(slot.Value);
        _userInterfaceManager.GetUIController<LobbyUIController>().ReloadCharacterSetup();
    }
}
