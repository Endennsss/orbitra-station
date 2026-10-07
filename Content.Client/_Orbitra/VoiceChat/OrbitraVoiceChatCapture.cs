using System;
using Content.Shared._Orbitra.VoiceChat;
using Robust.Client.Audio;

namespace Content.Client._Orbitra.VoiceChat;

/// <summary>
/// Owns the OpenAL capture device and exposes complete voice frames only.
/// </summary>
internal sealed class OrbitraVoiceChatCapture : IDisposable
{
    private const int CaptureBufferSamples = OrbitraVoiceChatPolicy.SampleRate / 5;
    private readonly VoiceChatCapture _capture = new();

    public bool IsCapturing => _capture.IsCapturing;

    public bool Start()
    {
        return _capture.Start(OrbitraVoiceChatPolicy.SampleRate, CaptureBufferSamples);
    }

    public bool TryReadFrame(short[] destination)
    {
        return _capture.TryReadFrame(destination, OrbitraVoiceChatPolicy.SamplesPerFrame);
    }

    public void Stop()
    {
        _capture.Stop();
    }

    public void Dispose()
    {
        _capture.Dispose();
    }
}
