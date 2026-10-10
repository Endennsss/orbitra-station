using System;
using System.Collections.Generic;
using Content.Shared._Orbitra.VoiceChat;
#if !FULL_RELEASE
using Robust.Client.Audio;
#endif

namespace Content.Client._Orbitra.VoiceChat;

/// <summary>
/// Owns the OpenAL capture device and exposes complete voice frames only.
/// </summary>
internal sealed class OrbitraVoiceChatCapture : IDisposable
{
    // Orbitra-Edit: запас в 500 мс переживает просадку FPS и не обрывает PTT
    // при временной задержке игрового потока.
    private const int CaptureBufferSamples = OrbitraVoiceChatPolicy.SampleRate / 2;
#if !FULL_RELEASE
    private VoiceChatCapture? _capture;
#endif

#if !FULL_RELEASE
    public bool IsCapturing => _capture?.IsCapturing == true;
#else
    public bool IsCapturing => false;
#endif

    public static IReadOnlyList<string> GetCaptureDevices()
    {
#if FULL_RELEASE
        // Orbitra-Edit: production content работает со штатным engine без native voice API.
        return Array.Empty<string>();
#else
        try
        {
            return VoiceChatCapture.GetCaptureDevices();
        }
        catch (Exception)
        {
            // Старый клиент может не содержать backend; список устройств не должен ломать настройки.
            return Array.Empty<string>();
        }
#endif
    }

    public bool Start(string? deviceName)
    {
#if FULL_RELEASE
        return false;
#else
        try
        {
            _capture ??= new VoiceChatCapture();
            return _capture.Start(OrbitraVoiceChatPolicy.SampleRate, CaptureBufferSamples, deviceName);
        }
        catch (Exception)
        {
            _capture?.Dispose();
            _capture = null;
            return false;
        }
#endif
    }

    public bool TryReadFrame(short[] destination)
    {
#if FULL_RELEASE
        return false;
#else
        return _capture?.TryReadFrame(destination, OrbitraVoiceChatPolicy.SamplesPerFrame) == true;
#endif
    }

    public void TrimBacklog(int maxFrames)
    {
#if !FULL_RELEASE
        _capture?.TrimToSamples(OrbitraVoiceChatPolicy.SamplesPerFrame * maxFrames);
#endif
    }

    public void Stop()
    {
#if !FULL_RELEASE
        // При отпускании PTT только ставим захват на паузу. Полное закрытие
        // OpenAL-устройства на каждом нажатии блокирует игровой поток при
        // спаме кнопкой; закрытие выполняется через Dispose или при смене
        // устройства внутри backend.
        _capture?.Pause();
#endif
    }

    public void Dispose()
    {
#if !FULL_RELEASE
        _capture?.Dispose();
        _capture = null;
#endif
    }
}
