using System;
using Content.Shared._Orbitra.VoiceChat;
using Robust.Client.Audio;

namespace Content.Client._Orbitra.VoiceChat;

/// <summary>
/// Small managed Opus wrapper used by the Orbitra client voice stream.
/// </summary>
internal sealed class OrbitraVoiceChatEncoder : IDisposable
{
    private readonly VoiceChatOpusEncoder _encoder;

    public OrbitraVoiceChatEncoder()
    {
        _encoder = new VoiceChatOpusEncoder(
            OrbitraVoiceChatPolicy.SampleRate,
            OrbitraVoiceChatPolicy.Channels);
    }

    public int Encode(short[] samples, byte[] destination)
    {
        return _encoder.Encode(
            samples,
            OrbitraVoiceChatPolicy.SamplesPerFrame,
            destination);
    }

    public void Dispose()
    {
        _encoder.Dispose();
    }
}

/// <summary>
/// Stateful decoder for one remote speaker.
/// </summary>
internal sealed class OrbitraVoiceChatDecoder : IDisposable
{
    private readonly VoiceChatOpusDecoder _decoder;

    public OrbitraVoiceChatDecoder()
    {
        _decoder = new VoiceChatOpusDecoder(
            OrbitraVoiceChatPolicy.SampleRate,
            OrbitraVoiceChatPolicy.Channels);
    }

    public int Decode(byte[] data, short[] destination)
    {
        return _decoder.Decode(data, destination);
    }

    public void Dispose()
    {
        _decoder.Dispose();
    }
}
