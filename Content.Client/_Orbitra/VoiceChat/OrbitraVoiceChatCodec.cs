using System;
using Concentus;
using Concentus.Enums;
using Content.Shared._Orbitra.VoiceChat;

namespace Content.Client._Orbitra.VoiceChat;

/// <summary>
/// Small managed Opus wrapper used by the Orbitra client voice stream.
/// </summary>
internal sealed class OrbitraVoiceChatEncoder : IDisposable
{
    private readonly IOpusEncoder _encoder;

    public OrbitraVoiceChatEncoder()
    {
        _encoder = OpusCodecFactory.CreateEncoder(
            OrbitraVoiceChatPolicy.SampleRate,
            OrbitraVoiceChatPolicy.Channels,
            OpusApplication.OPUS_APPLICATION_VOIP);
    }

    public int Encode(short[] samples, byte[] destination)
    {
        return _encoder.Encode(
            samples.AsSpan(),
            OrbitraVoiceChatPolicy.SamplesPerFrame,
            destination.AsSpan(),
            destination.Length);
    }

    public void Dispose()
    {
        (_encoder as IDisposable)?.Dispose();
    }
}

/// <summary>
/// Stateful decoder for one remote speaker.
/// </summary>
internal sealed class OrbitraVoiceChatDecoder : IDisposable
{
    private readonly IOpusDecoder _decoder;

    public OrbitraVoiceChatDecoder()
    {
        _decoder = OpusCodecFactory.CreateDecoder(
            OrbitraVoiceChatPolicy.SampleRate,
            OrbitraVoiceChatPolicy.Channels);
    }

    public int Decode(byte[] data, short[] destination)
    {
        return _decoder.Decode(
            data.AsSpan(),
            destination.AsSpan(),
            destination.Length,
            false);
    }

    public void Dispose()
    {
        (_decoder as IDisposable)?.Dispose();
    }
}
