using Content.Server._Orbitra.Ratvar;
using Content.Shared.Chat;
using Content.Shared.Players.RateLimiting;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    [Dependency] private OrbitraRatvarRuleSystem _orbitraRatvarRule = default!;

    private bool TryHandleOrbitraRatvarChat(EntityUid source, string message, InGameICChatType type,
        IConsoleShell? shell, ICommonSession? player)
    {
        if (type != InGameICChatType.Speak && type != InGameICChatType.Whisper) return false;
        var trimmed = message.TrimStart();
        if (trimmed.Length < 2 || trimmed[0] != '+' ||
            trimmed[1] is not ('р' or 'Р' or 'r' or 'R') ||
            trimmed.Length > 2 && !char.IsWhiteSpace(trimmed[2])) return false;

        // Распознанный префикс поглощается даже при отказе, чтобы тайное сообщение не ушло в обычный чат.
        if (player != null && (player.AttachedEntity != source ||
                _chatManager.HandleRateLimit(player) != RateLimitStatus.Allowed)) return true;
        if (!CanSendInGame(message, shell, player)) return true;
        if (!_orbitraRatvarRule.TrySendHiveMessage(source, trimmed[2..]) && player != null)
            _chatManager.DispatchServerMessage(player, Loc.GetString("orbitra-ratvar-communication-denied"));
        return true;
    }
}
