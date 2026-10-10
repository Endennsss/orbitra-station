using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared._Orbitra.VoiceChat;
using Content.Shared.GameTicking;
using Content.Shared.Input;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
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
    private const int MaxCaptureFramesPerUpdate = 4;
    private static readonly TimeSpan CaptureRecoveryInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PlaybackGapReset = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan DecoderIdleTimeout = TimeSpan.FromSeconds(2);

    // Система создаётся заново после переподключения, а NetManager живёт дольше неё.
    private static OrbitraVoiceChatSystem? _activeInstance;

    [Dependency] private IAudioManager _audio = default!;
    [Dependency] private IClientNetManager _net = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _logManager = default!;

    private readonly Dictionary<NetEntity, DecoderState> _decoders = new();
    private readonly List<NetEntity> _idleDecoders = new();
    private readonly short[] _captureFrame = new short[OrbitraVoiceChatPolicy.SamplesPerFrame];
    private readonly byte[] _encodedFrame = new byte[OrbitraVoiceChatPolicy.MaxEncodedFrameBytes];
    private readonly short[] _decodedFrame = new short[OrbitraVoiceChatPolicy.SamplesPerFrame * 6];

    private OrbitraVoiceChatCapture? _capture;
    private OrbitraVoiceChatEncoder? _encoder;
    private OrbitraVoiceSpeakerOverlay _speakerOverlay = default!;
    private OrbitraVoiceSpeakerCardControl _speakerCardControl = default!;
    private bool _speakerOverlayAdded;
    private bool _speakerCardAdded;
    private ISawmill _sawmill = default!;
    private ushort _sequence;
    private bool _speaking;
    private OrbitraVoiceTransmissionMode _speakingMode;
    private string _speakingRadioChannel = string.Empty;
    private ChatUIController? _chatController;
    private TimeSpan _nextCaptureRecoveryAt;
    private float _voiceInputGain;
    private float _voiceGain;
    private bool _voiceEnabled;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("orbitra.voice");
        _cfg.OnValueChanged(OrbitraVoiceChatCVars.InputVolume, OnVoiceInputVolumeChanged, true);
        _cfg.OnValueChanged(OrbitraVoiceChatCVars.VoiceVolume, OnVoiceVolumeChanged, true);
        _cfg.OnValueChanged(OrbitraVoiceChatCVars.VoiceEnabled, OnVoiceEnabledChanged, true);
        if (_activeInstance is { } previous && !ReferenceEquals(previous, this))
        {
            previous.RemoveSpeakerCardControl();
            previous.RemoveSpeakerOverlay();
        }

        _activeInstance = this;
        _speakerOverlay = new OrbitraVoiceSpeakerOverlay(_timing, _resources, EntityManager, _player, _uiManager);
        _overlayManager.AddOverlay(_speakerOverlay);
        _speakerOverlayAdded = true;
        _speakerCardControl = new OrbitraVoiceSpeakerCardControl(_speakerOverlay);
        LayoutContainer.SetAnchorPreset(_speakerCardControl, LayoutContainer.LayoutPreset.Wide);
        _uiManager.PopupRoot.AddChild(_speakerCardControl);
        _speakerCardAdded = true;
        _net.Disconnect += OnDisconnected;
        _chatController = _uiManager.GetUIController<ChatUIController>();
        _chatController.SelectedRadioChannelChanged += OnSelectedRadioChannelChanged;
        _input.SetInputCommand(
            ContentKeyFunctions.PushToTalk,
            InputCmdHandler.FromDelegate(StartProximitySpeaking, StopProximitySpeaking));
        _input.SetInputCommand(
            ContentKeyFunctions.RadioPushToTalk,
            InputCmdHandler.FromDelegate(StartRadioSpeaking, StopRadioSpeaking));
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => ResetVoiceState());
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var decoder in _decoders.Values)
            decoder.Playback?.Pump();

        RemoveIdleDecoders();

        if (!_speaking || _capture == null || _encoder == null)
            return;

        if (!_capture.IsCapturing)
        {
            TryRecoverCapture();
            return;
        }

        // При низком FPS обрабатываем несколько свежих кадров за один update,
        // но не позволяем очереди OpenAL превращаться в растущую задержку.
        _capture.TrimBacklog(MaxCaptureFramesPerUpdate);
        var framesProcessed = 0;
        while (framesProcessed++ < MaxCaptureFramesPerUpdate && _capture.TryReadFrame(_captureFrame))
        {
            OrbitraVoiceChatPolicy.ApplyInputGain(_captureFrame, _voiceInputGain);

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
                TransmissionMode = _speakingMode,
                RadioChannelId = _speakingRadioChannel,
                Data = payload
            });
        }
    }

    public override void Shutdown()
    {
        ResetVoiceState();
        RemoveSpeakerCardControl();
        RemoveSpeakerOverlay();
        _net.Disconnect -= OnDisconnected;
        if (_chatController != null)
            _chatController.SelectedRadioChannelChanged -= OnSelectedRadioChannelChanged;
        _cfg.UnsubValueChanged(OrbitraVoiceChatCVars.InputVolume, OnVoiceInputVolumeChanged);
        _cfg.UnsubValueChanged(OrbitraVoiceChatCVars.VoiceVolume, OnVoiceVolumeChanged);
        _cfg.UnsubValueChanged(OrbitraVoiceChatCVars.VoiceEnabled, OnVoiceEnabledChanged);
        _input.SetInputCommand(ContentKeyFunctions.PushToTalk, null);
        _input.SetInputCommand(ContentKeyFunctions.RadioPushToTalk, null);

        if (ReferenceEquals(_activeInstance, this))
            _activeInstance = null;

        base.Shutdown();
    }

    private void RemoveSpeakerOverlay()
    {
        if (!_speakerOverlayAdded)
            return;

        _overlayManager.RemoveOverlay(_speakerOverlay);
        _speakerOverlayAdded = false;
    }

    private void RemoveSpeakerCardControl()
    {
        if (!_speakerCardAdded)
            return;

        _uiManager.PopupRoot.RemoveChild(_speakerCardControl);
        _speakerCardAdded = false;
    }

    internal static void OnVoiceFrameStatic(MsgOrbitraVoiceFrame message)
    {
        _activeInstance?.OnVoiceFrame(message);
    }

    private void ResetVoiceState()
    {
        StopSpeaking(null);

        _capture?.Dispose();
        _capture = null;
        _encoder?.Dispose();
        _encoder = null;

        foreach (var decoder in _decoders.Values)
            decoder.Dispose();
        _decoders.Clear();

        _speakerOverlay.Clear();
    }

    private void OnDisconnected(object? sender, NetDisconnectedArgs args)
    {
        ResetVoiceState();
    }

    private void StartProximitySpeaking(ICommonSession? _)
    {
        StartSpeaking(OrbitraVoiceTransmissionMode.Proximity, string.Empty);
    }

    private void StartRadioSpeaking(ICommonSession? _)
    {
        var channel = _chatController?.ResolveSelectedRadioChannel();
        if (channel == null)
        {
            _sawmill.Debug("Radio push-to-talk was pressed without an available radio channel.");
            return;
        }

        StartSpeaking(OrbitraVoiceTransmissionMode.Radio, channel.ID);
    }

    private void StartSpeaking(OrbitraVoiceTransmissionMode mode, string radioChannelId)
    {
        if (_speaking)
            return;

        _capture ??= new OrbitraVoiceChatCapture();
        if (!_capture.Start(_cfg.GetCVar(OrbitraVoiceChatCVars.InputDevice)))
        {
            _sawmill.Warning("Push-to-talk was pressed, but no microphone capture device is available.");
            return;
        }

        try
        {
            _encoder ??= new OrbitraVoiceChatEncoder();
            // Не сбрасываем sequence между нажатиями: сервер хранит последнее число до отключения.
            _speaking = true;
            _speakingMode = mode;
            _speakingRadioChannel = radioChannelId;
            _speakerOverlay.ShowLocal(GetLocalSpeakerName());
        }
        catch (Exception e)
        {
            _sawmill.Warning("Unable to initialize the Opus encoder: {0}", e);
            _encoder?.Dispose();
            _encoder = null;
            StopSpeaking(null);
        }
    }

    private void StopSpeaking(ICommonSession? _)
    {
        _speaking = false;
        _speakingMode = OrbitraVoiceTransmissionMode.Proximity;
        _speakingRadioChannel = string.Empty;
        _speakerOverlay.HideLocal();
        _capture?.Stop();
    }

    private void StopProximitySpeaking(ICommonSession? _)
    {
        if (_speaking && _speakingMode == OrbitraVoiceTransmissionMode.Proximity)
            StopSpeaking(null);
    }

    private void StopRadioSpeaking(ICommonSession? _)
    {
        if (_speaking && _speakingMode == OrbitraVoiceTransmissionMode.Radio)
            StopSpeaking(null);
    }

    private void OnSelectedRadioChannelChanged(Content.Shared.Radio.RadioChannelPrototype? channel)
    {
        if (_speaking && _speakingMode == OrbitraVoiceTransmissionMode.Radio &&
            !string.Equals(_speakingRadioChannel, channel?.ID, StringComparison.Ordinal))
        {
            StopSpeaking(null);
        }
    }

    private void TryRecoverCapture()
    {
        var now = _timing.RealTime;
        if (now < _nextCaptureRecoveryAt || _capture == null)
            return;

        _nextCaptureRecoveryAt = now + CaptureRecoveryInterval;
        if (_capture.Start(_cfg.GetCVar(OrbitraVoiceChatCVars.InputDevice)))
            _sawmill.Info("Microphone capture recovered while push-to-talk was held.");
        else
            _sawmill.Warning("Microphone capture stopped while push-to-talk was held; retrying.");
    }

    private void OnVoiceFrame(MsgOrbitraVoiceFrame message)
    {
        if (message.Speaker == NetEntity.Invalid ||
            !OrbitraVoiceChatPolicy.IsValidPayloadLength(message.Data.Length) ||
            !OrbitraVoiceChatPolicy.IsValidTransmission(message.TransmissionMode, message.RadioChannelId))
            return;

        if (!_voiceEnabled || (_decoders.Count >= MaxActiveSources && !_decoders.ContainsKey(message.Speaker)))
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

        var now = _timing.RealTime;

        if (decoderState.LastFrameAt != TimeSpan.Zero && now - decoderState.LastFrameAt >= PlaybackGapReset)
            decoderState.ClearPlaybackBuffer();

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
        decoderState.LastFrameAt = now;
        if (decodedSamples <= 0)
            return;

        if (decoderState.LastMode != message.TransmissionMode)
        {
            decoderState.LastMode = message.TransmissionMode;
            decoderState.RadioFilterState = 0;
        }

        if (message.TransmissionMode == OrbitraVoiceTransmissionMode.Radio)
            OrbitraRadioVoiceFilter.Apply(_decodedFrame.AsSpan(0, decodedSamples), ref decoderState.RadioFilterState);

        decoderState.Name ??= GetSpeakerName(message.Speaker);
        _speakerOverlay.TouchSpeaker(message.Speaker, decoderState.Name, message.TransmissionMode, message.RadioChannelId);

        try
        {
            decoderState.Playback ??= new OrbitraVoicePlaybackStream(
                _audio,
                message.Position,
                _voiceGain,
                message.TransmissionMode == OrbitraVoiceTransmissionMode.Radio);
            decoderState.Playback.SetRadioMode(message.TransmissionMode == OrbitraVoiceTransmissionMode.Radio);
            decoderState.Playback.SetPosition(message.Position);
            decoderState.Playback.Enqueue(_decodedFrame.AsSpan(0, decodedSamples));
        }
        catch (Exception e)
        {
            decoderState.Playback?.Dispose();
            decoderState.Playback = null;
            _sawmill.Debug("Unable to play a decoded voice frame: {0}", e);
        }
    }

    private void OnVoiceVolumeChanged(float volume)
    {
        _voiceGain = Math.Clamp(volume, 0f, 2f);
        foreach (var decoder in _decoders.Values)
            decoder.Playback?.SetGain(_voiceEnabled ? _voiceGain : 0f);
    }

    private void OnVoiceInputVolumeChanged(float volume)
    {
        _voiceInputGain = Math.Clamp(volume, 0f, 2f);
    }

    private void OnVoiceEnabledChanged(bool enabled)
    {
        _voiceEnabled = enabled;
        foreach (var decoder in _decoders.Values)
            decoder.Playback?.SetGain(enabled ? _voiceGain : 0f);
    }

    private void RemoveIdleDecoders()
    {
        var now = _timing.RealTime;
        _idleDecoders.Clear();
        foreach (var (speaker, decoder) in _decoders)
        {
            if (decoder.LastFrameAt != TimeSpan.Zero && now - decoder.LastFrameAt >= DecoderIdleTimeout)
                _idleDecoders.Add(speaker);
        }

        foreach (var speaker in _idleDecoders)
        {
            if (_decoders.Remove(speaker, out var decoder))
                decoder.Dispose();
        }
    }

    private string GetLocalSpeakerName()
    {
        if (_player.LocalEntity is { } entity && TryComp(entity, out MetaDataComponent? metadata) &&
            !string.IsNullOrWhiteSpace(metadata.EntityName))
        {
            return metadata.EntityName;
        }

        return Loc.GetString("orbitra-voice-chat-you");
    }

    private string GetSpeakerName(NetEntity speaker)
    {
        var entity = GetEntity(speaker);
        if (Exists(entity) && TryComp(entity, out MetaDataComponent? metadata) &&
            !string.IsNullOrWhiteSpace(metadata.EntityName))
        {
            return metadata.EntityName;
        }

        return Loc.GetString("orbitra-voice-chat-unknown-speaker");
    }

    private sealed class DecoderState : IDisposable
    {
        public readonly OrbitraVoiceChatDecoder Decoder;
        public OrbitraVoicePlaybackStream? Playback;
        public ushort? LastSequence;
        public TimeSpan LastFrameAt;
        public string? Name;
        public OrbitraVoiceTransmissionMode LastMode;
        public uint RadioFilterState;

        public DecoderState(OrbitraVoiceChatDecoder decoder)
        {
            Decoder = decoder;
        }

        public void ClearPlaybackBuffer()
        {
            Playback?.Dispose();
            Playback = null;
        }

        public void Dispose()
        {
            Playback?.Dispose();
            Decoder.Dispose();
        }
    }
}
