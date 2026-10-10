using System;
using System.Collections.Generic;
using Content.Shared._Orbitra.VoiceChat;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.Radio.EntitySystems;
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
    [Dependency] private RadioSystem _radio = default!;

    private readonly Dictionary<NetUserId, SpeakerState> _speakers = new();
    private readonly List<INetChannel> _recipients = new();

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

        if (!OrbitraVoiceChatPolicy.IsValidTransmission(message.TransmissionMode, message.RadioChannelId))
            return;

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
        Filter? filter = null;
        if (message.TransmissionMode == OrbitraVoiceTransmissionMode.Proximity)
        {
            filter = Filter.Empty()
                .AddInRange(coordinates, OrbitraVoiceChatPolicy.ProximityRange, _players, EntityManager)
                .RemovePlayer(session);

            if (filter.Count == 0)
                return;
        }

        var outgoing = new MsgOrbitraVoiceFrame
        {
            Sequence = message.Sequence,
            Speaker = GetNetEntity(speaker),
            Position = coordinates.Position,
            TransmissionMode = message.TransmissionMode,
            RadioChannelId = message.RadioChannelId,
            Data = message.Data
        };

        _recipients.Clear();
        if (message.TransmissionMode == OrbitraVoiceTransmissionMode.Radio)
        {
            if (!_radio.TryCollectVoiceRecipients(speaker, message.RadioChannelId, _recipients))
                return;
        }
        else
        {
            foreach (var player in filter!.Recipients)
            {
                if (player.Channel.IsConnected)
                    _recipients.Add(player.Channel);
            }
        }

        if (_recipients.Count != 0)
            _net.ServerSendToMany(outgoing, _recipients);
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
