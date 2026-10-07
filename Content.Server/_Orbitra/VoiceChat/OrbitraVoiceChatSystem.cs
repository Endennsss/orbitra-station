using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._Orbitra.VoiceChat;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Network;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.VoiceChat;

/// <summary>
/// Validates client voice frames and routes them to living players in proximity.
/// </summary>
public sealed partial class OrbitraVoiceChatSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IServerNetManager _net = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<NetUserId, SpeakerState> _speakers = new();

    public override void Initialize()
    {
        base.Initialize();
        _net.RegisterNetMessage<MsgOrbitraVoiceFrame>(OnVoiceFrame, NetMessageAccept.Server);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _speakers.Clear());
    }

    public override void Shutdown()
    {
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
        _speakers.Clear();
        base.Shutdown();
    }

    private void OnVoiceFrame(MsgOrbitraVoiceFrame message)
    {
        if (!_players.TryGetSessionByChannel(message.MsgChannel, out var session) ||
            session.AttachedEntity is not { Valid: true } speaker ||
            !Exists(speaker) ||
            !_mobState.IsAlive(speaker) ||
            !OrbitraVoiceChatPolicy.IsValidPayloadLength(message.Data.Length))
        {
            return;
        }

        if (!_speakers.TryGetValue(session.UserId, out var state))
        {
            state = new SpeakerState();
            _speakers.Add(session.UserId, state);
        }

        if (state.LastSequence.HasValue &&
            !OrbitraVoiceChatPolicy.IsSequenceNewer(state.LastSequence.Value, message.Sequence))
        {
            return;
        }

        if (!state.RateLimiter.TryAccept(_timing.RealTime))
            return;

        state.LastSequence = message.Sequence;

        var coordinates = _transform.GetMapCoordinates(speaker);
        var filter = Filter.Empty()
            .AddInRange(coordinates, OrbitraVoiceChatPolicy.ProximityRange, _players, EntityManager)
            .RemovePlayer(session);

        if (filter.Count == 0)
            return;

        var outgoing = new MsgOrbitraVoiceFrame
        {
            Sequence = message.Sequence,
            Speaker = GetNetEntity(speaker),
            Position = coordinates.Position,
            Data = message.Data
        };

        var recipients = filter.Recipients
            .Select(player => player.Channel)
            .Where(channel => channel.IsConnected)
            .ToList();

        if (recipients.Count != 0)
            _net.ServerSendToMany(outgoing, recipients);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            _speakers.Remove(args.Session.UserId);
    }

    private sealed class SpeakerState
    {
        public readonly OrbitraVoiceChatRateLimiter RateLimiter = new();
        public ushort? LastSequence;
    }
}
