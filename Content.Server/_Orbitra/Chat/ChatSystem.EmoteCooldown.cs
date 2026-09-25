using Content.Shared.GameTicking;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan OrbitraEmoteCooldown = TimeSpan.FromSeconds(2);
    private readonly Dictionary<ICommonSession, TimeSpan> _orbitraNextEmotes = new();

    private void InitializeOrbitraEmoteCooldown()
    {
        _playerManager.PlayerStatusChanged += OnOrbitraEmoteSessionStatus;
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _orbitraNextEmotes.Clear());
    }

    public override void Shutdown()
    {
        _playerManager.PlayerStatusChanged -= OnOrbitraEmoteSessionStatus;
        _orbitraNextEmotes.Clear();
        base.Shutdown();
    }

    private void OnOrbitraEmoteSessionStatus(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            _orbitraNextEmotes.Remove(args.Session);
    }

    /// <inheritdoc />
    public override bool TryConsumeOrbitraEmote(ICommonSession player)
    {
        if (!CanUseOrbitraEmote(player))
            return false;

        _orbitraNextEmotes[player] = _timing.RealTime + OrbitraEmoteCooldown;
        return true;
    }

    private bool CanUseOrbitraEmote(ICommonSession player)
    {
        return player.Status != SessionStatus.Disconnected &&
               (!_orbitraNextEmotes.TryGetValue(player, out var next) || _timing.RealTime >= next);
    }
}
