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
    public const int MaxFramesPerSecond = 60;
    public const float ProximityRange = 7f;

    public static readonly TimeSpan MinimumFrameInterval =
        TimeSpan.FromSeconds(1d / MaxFramesPerSecond);

    public static bool IsValidPayloadLength(int length) =>
        length is > 0 and <= MaxEncodedFrameBytes;

    public static bool IsWithinProximity(MapCoordinates speaker, MapCoordinates listener) =>
        speaker.MapId == listener.MapId &&
        (listener.Position - speaker.Position).LengthSquared() < ProximityRange * ProximityRange;

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
    private bool _hasAcceptedFrame;
    private TimeSpan _lastAccepted;

    public bool TryAccept(TimeSpan timestamp)
    {
        if (timestamp < TimeSpan.Zero)
            return false;

        if (_hasAcceptedFrame && timestamp - _lastAccepted < OrbitraVoiceChatPolicy.MinimumFrameInterval)
            return false;

        _lastAccepted = timestamp;
        _hasAcceptedFrame = true;
        return true;
    }

    public void Reset()
    {
        _hasAcceptedFrame = false;
        _lastAccepted = default;
    }
}
