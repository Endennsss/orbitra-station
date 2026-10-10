using System;
using Content.Shared._Orbitra.VoiceChat;
#if !FULL_RELEASE
using Robust.Client.Audio;
#endif

namespace Content.Client._Orbitra.VoiceChat;

/// <summary>
/// Small managed Opus wrapper used by the Orbitra client voice stream.
/// </summary>
internal sealed class OrbitraVoiceChatEncoder : IDisposable
{
#if !FULL_RELEASE
    private readonly VoiceChatOpusEncoder _encoder;
#endif

    public OrbitraVoiceChatEncoder()
    {
#if !FULL_RELEASE
        _encoder = new VoiceChatOpusEncoder(
            OrbitraVoiceChatPolicy.SampleRate,
            OrbitraVoiceChatPolicy.Channels);
#endif
    }

    public int Encode(short[] samples, byte[] destination)
    {
#if FULL_RELEASE
        return 0;
#else
        return _encoder.Encode(
            samples,
            OrbitraVoiceChatPolicy.SamplesPerFrame,
            destination);
#endif
    }

    public void Dispose()
    {
#if !FULL_RELEASE
        _encoder.Dispose();
#endif
    }
}

/// <summary>
/// Stateful decoder for one remote speaker.
/// </summary>
internal sealed class OrbitraVoiceChatDecoder : IDisposable
{
#if !FULL_RELEASE
    private readonly VoiceChatOpusDecoder _decoder;
#endif

    public OrbitraVoiceChatDecoder()
    {
#if !FULL_RELEASE
        _decoder = new VoiceChatOpusDecoder(
            OrbitraVoiceChatPolicy.SampleRate,
            OrbitraVoiceChatPolicy.Channels);
#endif
    }

    public int Decode(byte[] data, short[] destination)
    {
#if FULL_RELEASE
        return 0;
#else
        return _decoder.Decode(data, destination);
#endif
    }

    public void Dispose()
    {
#if !FULL_RELEASE
        _decoder.Dispose();
#endif
    }
}
