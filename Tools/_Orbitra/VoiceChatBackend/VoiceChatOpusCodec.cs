using Concentus;
using Concentus.Enums;

namespace Robust.Client.Audio;

/// <summary>
/// Managed Opus encoder used by the Orbitra client voice stream.
/// </summary>
public sealed class VoiceChatOpusEncoder : IDisposable
{
    private readonly IOpusEncoder _encoder;

    public VoiceChatOpusEncoder(int sampleRate, int channels)
    {
        _encoder = OpusCodecFactory.CreateEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_VOIP);
    }

    public int Encode(short[] samples, int sampleCount, byte[] destination)
    {
        return _encoder.Encode(
            samples.AsSpan(0, sampleCount),
            sampleCount,
            destination.AsSpan(),
            destination.Length);
    }

    public void Dispose()
    {
        (_encoder as IDisposable)?.Dispose();
    }
}

/// <summary>
/// Managed Opus decoder used by the Orbitra client voice stream.
/// </summary>
public sealed class VoiceChatOpusDecoder : IDisposable
{
    private readonly IOpusDecoder _decoder;

    public VoiceChatOpusDecoder(int sampleRate, int channels)
    {
        _decoder = OpusCodecFactory.CreateDecoder(sampleRate, channels);
    }

    public int Decode(byte[] data, short[] destination)
    {
        return _decoder.Decode(data.AsSpan(), destination.AsSpan(), destination.Length, false);
    }

    public void Dispose()
    {
        (_decoder as IDisposable)?.Dispose();
    }
}
