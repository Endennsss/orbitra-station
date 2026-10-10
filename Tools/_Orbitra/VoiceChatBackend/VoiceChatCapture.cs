using OpenTK.Audio.OpenAL;

namespace Robust.Client.Audio;

/// <summary>
/// Provides microphone capture to content code through a host-loaded backend.
/// </summary>
public sealed class VoiceChatCapture : IDisposable
{
    private ALCaptureDevice _device;
    private string? _deviceName;
    private bool _capturing;
    private readonly short[] _discardBuffer = new short[4096];

    public bool IsCapturing => _capturing;

    public static IReadOnlyList<string> GetCaptureDevices()
    {
        try
        {
            if (!ALC.IsCaptureExtensionPresent(ALDevice.Null))
                return Array.Empty<string>();

            return ALC.GetStringList(GetEnumerationStringList.CaptureDeviceSpecifier).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public bool Start(int sampleRate, int captureBufferSamples, string? deviceName = null)
    {
        if (_capturing)
            return true;

        if (sampleRate <= 0 || captureBufferSamples <= 0)
            return false;

        var requestedDevice = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName;
        if (_device != ALCaptureDevice.Null && !string.Equals(_deviceName, requestedDevice, StringComparison.Ordinal))
            Stop();

        try
        {
            if (_device == ALCaptureDevice.Null)
            {
                _device = ALC.CaptureOpenDevice(requestedDevice, sampleRate, ALFormat.Mono16, captureBufferSamples);
                if (_device == ALCaptureDevice.Null && requestedDevice != null)
                {
                    // Устройство могло исчезнуть после сохранения настройки; возвращаемся к системному default.
                    _device = ALC.CaptureOpenDevice(null, sampleRate, ALFormat.Mono16, captureBufferSamples);
                    requestedDevice = null;
                }

                _deviceName = requestedDevice;
            }

            if (_device == ALCaptureDevice.Null)
                return false;

            ALC.CaptureStart(_device);
            // Push-to-talk пауза не должна переносить старый звук в следующий
            // отрезок речи. Очищаем накопившийся хвост после запуска устройства.
            ClearPendingSamples();
            _capturing = true;
            return true;
        }
        catch
        {
            Stop();
            return false;
        }
    }

    public void Pause()
    {
        if (!_capturing)
            return;

        try
        {
            ALC.CaptureStop(_device);
        }
        finally
        {
            _capturing = false;
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

    public void TrimToSamples(int keepSamples)
    {
        if (!_capturing || keepSamples <= 0)
            return;

        try
        {
            var available = ALC.GetInteger(_device, AlcGetInteger.CaptureSamples);
            var staleSamples = available - Math.Max(keepSamples, 0);
            while (staleSamples > 0)
            {
                var discardCount = Math.Min(staleSamples, _discardBuffer.Length);
                ALC.CaptureSamples(_device, _discardBuffer, discardCount);
                staleSamples -= discardCount;
            }
        }
        catch
        {
            Stop();
        }
    }

    private void ClearPendingSamples()
    {
        var available = ALC.GetInteger(_device, AlcGetInteger.CaptureSamples);
        while (available > 0)
        {
            var discardCount = Math.Min(available, _discardBuffer.Length);
            ALC.CaptureSamples(_device, _discardBuffer, discardCount);
            available -= discardCount;
        }
    }

    public void Stop()
    {
        if (_device == ALCaptureDevice.Null)
        {
            _capturing = false;
            _deviceName = null;
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
            _deviceName = null;
            _capturing = false;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
