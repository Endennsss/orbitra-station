using OpenTK.Audio.OpenAL;

namespace Robust.Client.Audio;

/// <summary>
/// Provides microphone capture to content code through a host-loaded backend.
/// </summary>
public sealed class VoiceChatCapture : IDisposable
{
    private ALCaptureDevice _device;
    private bool _capturing;

    public bool IsCapturing => _capturing;

    public bool Start(int sampleRate, int captureBufferSamples)
    {
        if (_capturing)
            return true;

        if (sampleRate <= 0 || captureBufferSamples <= 0)
            return false;

        try
        {
            _device = ALC.CaptureOpenDevice(null, sampleRate, ALFormat.Mono16, captureBufferSamples);
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

    public bool TryReadFrame(short[] destination, int sampleCount)
    {
        if (!_capturing || sampleCount <= 0 || destination.Length < sampleCount)
            return false;

        try
        {
            var available = ALC.GetInteger(_device, AlcGetInteger.CaptureSamples);
            if (available < sampleCount)
                return false;

            ALC.CaptureSamples(_device, destination, sampleCount);
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
