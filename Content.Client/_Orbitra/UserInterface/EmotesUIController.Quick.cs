using Content.Shared.Chat.Prototypes;
using Content.Shared.Speech;
using Content.Shared.Whitelist;

namespace Content.Client.UserInterface.Systems.Emotes;

public sealed partial class EmotesUIController
{
    /// <summary>Использует для быстрых действий чата те же правила доступности, что и радиальное меню.</summary>
    public IEnumerable<EmotePrototype> GetAvailableEmotes()
    {
        foreach (var emote in _prototypeManager.EnumeratePrototypes<EmotePrototype>())
            if (CanUseEmote(emote))
                yield return emote;
    }

    private bool CanUseEmote(EmotePrototype emote)
    {
        var player = _playerManager.LocalSession?.AttachedEntity;
        if (player == null || emote.Category == EmoteCategory.Invalid || emote.ChatTriggers.Count == 0)
            return false;
        var whitelist = EntitySystemManager.GetEntitySystem<EntityWhitelistSystem>();
        if (!whitelist.IsWhitelistPassOrNull(emote.Whitelist, player.Value) || whitelist.IsWhitelistPass(emote.Blacklist, player.Value))
            return false;
        return emote.Available || !EntityManager.TryGetComponent<SpeechComponent>(player.Value, out var speech) || speech.AllowedEmotes.Contains(emote.ID);
    }

    /// <summary>Повторно проверяет быстрое действие и отправляет штатный запрос радиального меню с предсказанием.</summary>
    public bool TryPlayQuickEmote(EmotePrototype emote)
    {
        if (!CanUseEmote(emote))
            return false;
        HandleRadialButtonClick(emote);
        return true;
    }
}
