using System;
using System.Collections.Generic;
using Content.Shared._Orbitra.VoiceChat;
using Robust.Client.Audio;

namespace Content.Client._Orbitra.VoiceChat;

/// <summary>
/// Owns the OpenAL capture device and exposes complete voice frames only.
/// </summary>
internal sealed class OrbitraVoiceChatCapture : IDisposable
{
    // Orbitra-Edit: запас в 500 мс переживает просадку FPS и не обрывает PTT
    // при временной задержке игрового потока.
    private const int CaptureBufferSamples = OrbitraVoiceChatPolicy.SampleRate / 2;
    private VoiceChatCapture? _capture;

    public bool IsCapturing => _capture?.IsCapturing == true;

    public static IReadOnlyList<string> GetCaptureDevices()
    {
        try
        {
            return VoiceChatCapture.GetCaptureDevices();
        }
        catch (Exception)
        {
            // Старый клиент может не содержать backend; список устройств не должен ломать настройки.
            return Array.Empty<string>();
        }
    }

    public bool Start(string? deviceName)
    {
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
    }

    public bool TryReadFrame(short[] destination)
    {
        return _capture?.TryReadFrame(destination, OrbitraVoiceChatPolicy.SamplesPerFrame) == true;
    }

    public void TrimBacklog(int maxFrames)
    {
        _capture?.TrimToSamples(OrbitraVoiceChatPolicy.SamplesPerFrame * maxFrames);
    }

    public void Stop()
    {
        // При отпускании PTT только ставим захват на паузу. Полное закрытие
        // OpenAL-устройства на каждом нажатии блокирует игровой поток при
        // спаме кнопкой; закрытие выполняется через Dispose или при смене
        // устройства внутри backend.
        _capture?.Pause();
    }

    public void Dispose()
    {
        _capture?.Dispose();
        _capture = null;
    }
}
