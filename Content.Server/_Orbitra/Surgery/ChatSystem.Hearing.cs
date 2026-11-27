using Content.Shared._Orbitra.Surgery;
using Content.Shared.Chat;
using Robust.Shared.Player;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    [Dependency] private OrbitraOrganSystem _orbitraOrgans = default!;

    internal enum SpeechReception : byte
    {
        Clear,
        Muffled,
        Unintelligible,
        OutOfRange,
    }

    internal SpeechReception GetSpeechReception(ChatChannel channel, EntityUid source, EntityUid? listener, ICChatRecipientData recipient)
    {
        if (channel is not (ChatChannel.Local or ChatChannel.Whisper) || recipient.Observer || source == listener)
            return SpeechReception.Clear;

        var factor = _orbitraOrgans.HearingRangeMultiplier(listener);
        if (factor == 0f)
            return SpeechReception.Unintelligible;

        var whisper = channel == ChatChannel.Whisper;
        if (factor < 1f && recipient.Range >= (whisper ? WhisperMuffledRange : VoiceRange) * factor)
            return SpeechReception.OutOfRange;

        return whisper && recipient.Range > WhisperClearRange * factor ? SpeechReception.Muffled : SpeechReception.Clear;
    }

    private bool HandleImpairedHearing(SpeechReception reception, ChatChannel channel, EntityUid source, bool hide, ICommonSession session)
    {
        if (reception == SpeechReception.OutOfRange)
            return true;
        if (reception != SpeechReception.Unintelligible)
            return false;

        var message = Loc.GetString("orbitra-surgery-organ-unintelligible");
        _chatManager.ChatMessageToOne(channel, message, message, source, hide, session.Channel);
        return true;
    }
}
