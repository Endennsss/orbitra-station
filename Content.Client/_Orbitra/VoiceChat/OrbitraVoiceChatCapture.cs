using System;
using OpenTK.Audio.OpenAL;
using Content.Shared._Orbitra.VoiceChat;

namespace Content.Client._Orbitra.VoiceChat;

/// <summary>
/// Owns the OpenAL capture device and exposes complete voice frames only.
/// </summary>
internal sealed class OrbitraVoiceChatCapture : IDisposable
{
    private const int CaptureBufferSamples = OrbitraVoiceChatPolicy.SampleRate / 5;
    private ALCaptureDevice _device;
    private bool _capturing;

    public bool IsCapturing => _capturing;

    public bool Start()
    {
        if (_capturing)
            return true;

        try
        {
            _device = ALC.CaptureOpenDevice(
                null,
                OrbitraVoiceChatPolicy.SampleRate,
                ALFormat.Mono16,
                CaptureBufferSamples);

            if (_device == ALCaptureDevice.Null)
                return false;

            ALC.CaptureStart(_device);
            _capturing = true;
            return true;
        }
        catch
        {
            Stop();
            return false;
        }
    }

    public bool TryReadFrame(short[] destination)
    {
        if (!_capturing || destination.Length < OrbitraVoiceChatPolicy.SamplesPerFrame)
            return false;

        try
        {
            var available = ALC.GetInteger(_device, AlcGetInteger.CaptureSamples);
            if (available < OrbitraVoiceChatPolicy.SamplesPerFrame)
                return false;

            ALC.CaptureSamples(_device, destination, OrbitraVoiceChatPolicy.SamplesPerFrame);
            return true;
        }
        catch
        {
            Stop();
            return false;
        }
    }

    public void Stop()
    {
        if (_device == ALCaptureDevice.Null)
        {
            _capturing = false;
            return;
        }

        try
        {
            if (_capturing)
                ALC.CaptureStop(_device);

            ALC.CaptureCloseDevice(_device);
        }
        finally
        {
            _device = ALCaptureDevice.Null;
            _capturing = false;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
