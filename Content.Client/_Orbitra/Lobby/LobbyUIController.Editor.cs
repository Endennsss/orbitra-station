using Content.Shared.Preferences;
using System.Linq;

namespace Content.Client.Lobby;

public sealed partial class LobbyUIController
{
    private void RequestOrbitraProfileTransition(Action continuation)
    {
        if (_profileEditor is { Profile: not null, IsDirty: true })
        {
            OpenSavePanel(continuation);
            return;
        }
        continuation();
    }

    private void OnOrbitraCreateCharacter()
    {
        RequestOrbitraProfileTransition(() =>
        {
            if (_preferencesManager.Preferences is not { } preferences || _preferencesManager.Settings is not { } settings)
                return;
            var previous = preferences.Characters.Keys.ToHashSet();
            if (!Enumerable.Range(0, settings.MaxCharacterSlots).Any(slot => !previous.Contains(slot)))
                return;
            _preferencesManager.CreateCharacter(HumanoidCharacterProfile.Random().WithJobFromCvar(_configurationManager));
            var created = _preferencesManager.Preferences.Characters.Keys.First(slot => !previous.Contains(slot));
            _preferencesManager.SelectCharacter(created);
            ReloadCharacterSetup();
        });
    }
}
