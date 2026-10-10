using System;
using Robust.Shared.Map;

namespace Content.Shared._Orbitra.VoiceChat;

/// <summary>
/// Shared limits for the first Orbitra proximity voice channel.
/// </summary>
public static class OrbitraVoiceChatPolicy
{
    public const int SampleRate = 48_000;
    public const int Channels = 1;
    public const int FrameDurationMilliseconds = 20;
    public const int SamplesPerFrame = SampleRate * FrameDurationMilliseconds / 1_000;
    public const int MaxEncodedFrameBytes = 1_500;
    public const int MaxRadioChannelIdLength = 32;
    public const int MaxFramesPerSecond = 60;
    public const float ProximityRange = 7f;

    public static readonly TimeSpan MinimumFrameInterval =
        TimeSpan.FromSeconds(1d / MaxFramesPerSecond);

    public static bool IsValidPayloadLength(int length) =>
        length is > 0 and <= MaxEncodedFrameBytes;

    public static bool IsKnownTransmissionMode(OrbitraVoiceTransmissionMode mode) =>
        mode is OrbitraVoiceTransmissionMode.Proximity or OrbitraVoiceTransmissionMode.Radio;

    public static bool IsValidTransmission(OrbitraVoiceTransmissionMode mode, string? radioChannelId)
    {
        if (!IsKnownTransmissionMode(mode))
            return false;

        if (mode == OrbitraVoiceTransmissionMode.Proximity)
            return string.IsNullOrEmpty(radioChannelId);

        return !string.IsNullOrWhiteSpace(radioChannelId) &&
               radioChannelId.Length <= MaxRadioChannelIdLength;
    }

    public static bool IsWithinProximity(MapCoordinates speaker, MapCoordinates listener) =>
        speaker.MapId == listener.MapId &&
        (listener.Position - speaker.Position).LengthSquared() < ProximityRange * ProximityRange;

    /// <summary>
    /// Applies the local microphone gain while keeping PCM samples in the valid range.
    /// </summary>
    public static void ApplyInputGain(Span<short> samples, float gain)
    {
        gain = Math.Clamp(gain, 0f, 2f);
        if (gain == 1f)
            return;

        for (var i = 0; i < samples.Length; i++)
        {
            var sample = samples[i] * gain;
            samples[i] = (short) Math.Clamp(sample, short.MinValue, short.MaxValue);
        }
    }

    /// <summary>
    /// Checks a modulo-16-bit sequence number, allowing packet loss and wrap-around.
    /// </summary>
    public static bool IsSequenceNewer(ushort previous, ushort current)
    {
        var delta = (ushort) (current - previous);
        return delta != 0 && delta <= ushort.MaxValue / 2;
    }
}

/// <summary>
/// Per-channel cadence limiter. It uses server receive time and does not trust client timestamps.
/// </summary>
public sealed class OrbitraVoiceChatRateLimiter
{
    // Короткий запас покрывает пачку кадров, которую получает сервер после
    // просадки FPS, но средняя скорость остаётся ограниченной 60 кадрами/с.
    private const double BurstCapacity = 6d;
    private double _tokens = BurstCapacity;
    private TimeSpan _lastTimestamp;
    private bool _initialized;

    public bool TryAccept(TimeSpan timestamp)
    {
        if (timestamp < TimeSpan.Zero)
            return false;

        if (_initialized && timestamp < _lastTimestamp)
            return false;

        if (!_initialized)
        {
            _initialized = true;
            _lastTimestamp = timestamp;
            _tokens -= 1d;
            return true;
        }

        var elapsed = (timestamp - _lastTimestamp).TotalSeconds;
        _tokens = Math.Min(BurstCapacity, _tokens + elapsed * OrbitraVoiceChatPolicy.MaxFramesPerSecond);
        _lastTimestamp = timestamp;
        if (_tokens < 1d)
            return false;

        _tokens -= 1d;
        return true;
    }

    public void Reset()
    {
        _initialized = false;
        _lastTimestamp = default;
        _tokens = BurstCapacity;
    }
}
