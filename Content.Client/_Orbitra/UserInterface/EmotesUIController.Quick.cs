using Content.Shared.Chat.Prototypes;
using Content.Shared.Speech;
using Content.Shared.Whitelist;

namespace Content.Client.UserInterface.Systems.Emotes;

public sealed partial class EmotesUIController
{
    /// <summary>Shares the radial menu's eligibility rules with the chat shortcuts.</summary>
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

    /// <summary>Revalidates a shortcut and uses the same predictive request as the radial menu.</summary>
    public bool TryPlayQuickEmote(EmotePrototype emote)
    {
        if (!CanUseEmote(emote))
            return false;
        HandleRadialButtonClick(emote);
        return true;
    }
}
