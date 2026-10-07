using System;
using System.Collections.Generic;
using Content.Shared._Orbitra.VoiceChat;
using Content.Shared.GameTicking;
using Content.Shared.Input;
using Robust.Client.Audio;
using Robust.Client.Input;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.VoiceChat;

/// <summary>
/// Captures push-to-talk audio and plays authoritative proximity frames.
/// </summary>
public sealed partial class OrbitraVoiceChatSystem : EntitySystem
{
    private const int MaxActiveSources = 32;

    [Dependency] private IAudioManager _audio = default!;
    [Dependency] private IClientNetManager _net = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _logManager = default!;

    private readonly Dictionary<NetEntity, DecoderState> _decoders = new();
    private readonly List<ActiveVoiceSource> _activeSources = new();
    private readonly short[] _captureFrame = new short[OrbitraVoiceChatPolicy.SamplesPerFrame];
    private readonly byte[] _encodedFrame = new byte[OrbitraVoiceChatPolicy.MaxEncodedFrameBytes];
    private readonly short[] _decodedFrame = new short[OrbitraVoiceChatPolicy.SamplesPerFrame * 6];

    private OrbitraVoiceChatCapture? _capture;
    private OrbitraVoiceChatEncoder? _encoder;
    private ISawmill _sawmill = default!;
    private ushort _sequence;
    private bool _speaking;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("orbitra.voice");
        _net.RegisterNetMessage<MsgOrbitraVoiceFrame>(OnVoiceFrame, NetMessageAccept.Client);
        _input.SetInputCommand(
            ContentKeyFunctions.PushToTalk,
            InputCmdHandler.FromDelegate(StartSpeaking, StopSpeaking));
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => ResetVoiceState());
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        CleanupAudioSources();

        if (!_speaking || _capture is not { IsCapturing: true } || _encoder == null)
            return;

        while (_capture.TryReadFrame(_captureFrame))
        {
            int encodedLength;
            try
            {
                encodedLength = _encoder.Encode(_captureFrame, _encodedFrame);
            }
            catch (Exception e)
            {
                _sawmill.Warning("Failed to encode a voice frame: {0}", e);
                StopSpeaking(null);
                return;
            }

            if (!OrbitraVoiceChatPolicy.IsValidPayloadLength(encodedLength))
                continue;

            var payload = new byte[encodedLength];
            Array.Copy(_encodedFrame, payload, encodedLength);
            _net.ClientSendMessage(new MsgOrbitraVoiceFrame
            {
                Sequence = _sequence++,
                Data = payload
            });
        }
    }

    public override void Shutdown()
    {
        ResetVoiceState();
        _input.SetInputCommand(ContentKeyFunctions.PushToTalk, null);

        base.Shutdown();
    }

    private void ResetVoiceState()
    {
        StopSpeaking(null);

        foreach (var decoder in _decoders.Values)
            decoder.Dispose();
        _decoders.Clear();

        while (_activeSources.Count != 0)
        {
            var source = _activeSources[^1];
            _activeSources.RemoveAt(_activeSources.Count - 1);
            source.Dispose();
        }
    }

    private void StartSpeaking(ICommonSession? _)
    {
        if (_speaking)
            return;

        _capture ??= new OrbitraVoiceChatCapture();
        if (!_capture.Start())
        {
            _sawmill.Warning("Push-to-talk was pressed, but no microphone capture device is available.");
            return;
        }

        try
        {
            _encoder = new OrbitraVoiceChatEncoder();
            // Не сбрасываем sequence между нажатиями: сервер хранит последнее число до отключения.
            _speaking = true;
        }
        catch (Exception e)
        {
            _sawmill.Warning("Unable to initialize the Opus encoder: {0}", e);
            StopSpeaking(null);
        }
    }

    private void StopSpeaking(ICommonSession? _)
    {
        _speaking = false;
        _capture?.Stop();
        _encoder?.Dispose();
        _encoder = null;
    }

    private void OnVoiceFrame(MsgOrbitraVoiceFrame message)
    {
        if (message.Speaker == NetEntity.Invalid || !OrbitraVoiceChatPolicy.IsValidPayloadLength(message.Data.Length))
            return;

        if (!_decoders.TryGetValue(message.Speaker, out var decoderState))
        {
            try
            {
                decoderState = new DecoderState(new OrbitraVoiceChatDecoder());
                _decoders.Add(message.Speaker, decoderState);
            }
            catch (Exception e)
            {
                _sawmill.Warning("Unable to initialize an Opus decoder: {0}", e);
                return;
            }
        }

        if (decoderState.LastSequence.HasValue &&
            !OrbitraVoiceChatPolicy.IsSequenceNewer(decoderState.LastSequence.Value, message.Sequence))
        {
            return;
        }

        int decodedSamples;
        try
        {
            decodedSamples = decoderState.Decoder.Decode(message.Data, _decodedFrame);
        }
        catch (Exception e)
        {
            _sawmill.Debug("Dropped malformed Opus voice frame: {0}", e);
            return;
        }

        decoderState.LastSequence = message.Sequence;
        if (decodedSamples <= 0)
            return;

        AudioStream? stream = null;
        IAudioSource? source = null;
        try
        {
            stream = _audio.LoadAudioRaw(
                _decodedFrame.AsSpan(0, decodedSamples),
                OrbitraVoiceChatPolicy.Channels,
                OrbitraVoiceChatPolicy.SampleRate,
                "Orbitra voice");
            source = _audio.CreateAudioSource(stream);
            if (source == null)
            {
                stream.Dispose();
                stream = null;
                return;
            }

            source.Global = false;
            source.Position = message.Position;
            source.MaxDistance = OrbitraVoiceChatPolicy.ProximityRange;
            source.ReferenceDistance = 1f;
            source.RolloffFactor = 1f;
            source.Gain = 1f;
            source.StartPlaying();
            _activeSources.Add(new ActiveVoiceSource(source, stream, _timing.RealTime + stream.Length + TimeSpan.FromMilliseconds(100)));
            source = null;
            stream = null;

            while (_activeSources.Count > MaxActiveSources)
            {
                var oldest = _activeSources[0];
                _activeSources.RemoveAt(0);
                oldest.Dispose();
            }
        }
        catch (Exception e)
        {
            source?.Dispose();
            stream?.Dispose();
            _sawmill.Debug("Unable to play a decoded voice frame: {0}", e);
        }
    }

    private void CleanupAudioSources()
    {
        for (var i = _activeSources.Count - 1; i >= 0; i--)
        {
            var active = _activeSources[i];
            if (active.Source.Playing && _timing.RealTime < active.ExpiresAt)
                continue;

            _activeSources.RemoveAt(i);
            active.Dispose();
        }
    }

    private sealed class DecoderState : IDisposable
    {
        public readonly OrbitraVoiceChatDecoder Decoder;
        public ushort? LastSequence;

        public DecoderState(OrbitraVoiceChatDecoder decoder)
        {
            Decoder = decoder;
        }

        public void Dispose()
        {
            Decoder.Dispose();
        }
    }

    private sealed class ActiveVoiceSource : IDisposable
    {
        public readonly IAudioSource Source;
        private readonly AudioStream _stream;
        public readonly TimeSpan ExpiresAt;

        public ActiveVoiceSource(IAudioSource source, AudioStream stream, TimeSpan expiresAt)
        {
            Source = source;
            _stream = stream;
            ExpiresAt = expiresAt;
        }

        public void Dispose()
        {
            Source.StopPlaying();
            Source.Dispose();
            _stream.Dispose();
        }
    }
}
