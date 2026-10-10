using System;
using System.Numerics;
using Content.Shared._Orbitra.VoiceChat;
using Robust.Client.Audio;
using Robust.Shared.Audio.Sources;

namespace Content.Client._Orbitra.VoiceChat;

/// <summary>
/// Feeds one OpenAL streaming source per speaker. OpenAL keeps consuming the
/// queued PCM while the game thread is rendering a slow frame.
/// </summary>
internal sealed class OrbitraVoicePlaybackStream : IDisposable
{
    private const int BufferCount = 12;
    private const int StartBufferCount = 4;
    private const int SamplesPerBuffer = OrbitraVoiceChatPolicy.SamplesPerFrame;
    // BufferedAudioSource получает моно-PCM через span с двумя половинами:
    // первая содержит сигнал, вторая резервирует размер второго канала.
    private const int BufferedSampleCount = SamplesPerBuffer * 2;

    private readonly IBufferedAudioSource _source;
    private readonly ushort[][] _pending = new ushort[BufferCount][];
    private readonly bool[] _queued = new bool[BufferCount];
    private readonly int[] _processedHandles = new int[BufferCount];
    private readonly int[] _queueHandle = new int[1];
    private int _pendingHead;
    private int _pendingCount;
    private int _queuedCount;
    private bool _hasStarted;
    private bool _disposed;

    public OrbitraVoicePlaybackStream(IAudioManager audio, Vector2 position, float gain, bool radio = false)
    {
        _source = audio.CreateBufferedAudioSource(BufferCount)
            ?? throw new InvalidOperationException("Unable to create an OpenAL voice stream.");

        for (var i = 0; i < BufferCount; i++)
            _pending[i] = new ushort[BufferedSampleCount];

        _source.SampleRate = 48_000;
        _source.Position = position;
        SetRadioMode(radio);
        _source.RolloffFactor = 1f;
        _source.Gain = gain;
    }

    public void SetPosition(Vector2 position) => _source.Position = position;

    public void SetGain(float gain) => _source.Gain = gain;

    public void SetRadioMode(bool radio)
    {
        _source.Global = radio;
        _source.MaxDistance = radio ? 1f : OrbitraVoiceChatPolicy.ProximityRange;
        _source.ReferenceDistance = 1f;
    }

    public void Enqueue(ReadOnlySpan<short> samples)
    {
        if (_disposed)
            return;

        while (!samples.IsEmpty)
        {
            var copyCount = Math.Min(samples.Length, SamplesPerBuffer);
            if (_pendingCount == BufferCount)
            {
                // Keep the newest speech when a connection stalls for longer
                // than the bounded jitter buffer.
                _pendingHead = (_pendingHead + 1) % BufferCount;
                _pendingCount--;
            }

            var index = (_pendingHead + _pendingCount) % BufferCount;
            for (var i = 0; i < copyCount; i++)
                _pending[index][i] = unchecked((ushort) samples[i]);
            _pending[index].AsSpan(copyCount).Clear();
            _pendingCount++;
            samples = samples[copyCount..];
        }

        Pump();
    }

    public void Pump()
    {
        if (_disposed)
            return;

        var processed = _source.GetNumberOfBuffersProcessed();
        if (processed > 0)
        {
            _source.GetBuffersProcessed(_processedHandles);
            for (var i = 0; i < processed; i++)
            {
                var handle = _processedHandles[i];
                if (_queued[handle])
                {
                    _queued[handle] = false;
                    _queuedCount--;
                }
            }
        }

        while (_pendingCount > 0 && _queuedCount < BufferCount)
        {
            var handle = FindFreeBuffer();
            if (handle < 0)
                break;

            var pending = _pending[_pendingHead];
            _source.WriteBuffer(handle, pending);
            _queueHandle[0] = handle;
            _source.QueueBuffers(_queueHandle);
            _queued[handle] = true;
            _queuedCount++;
            _pendingHead = (_pendingHead + 1) % BufferCount;
            _pendingCount--;
        }

        // После сетевой паузы OpenAL может доиграть очередь до нуля. В этом
        // состоянии ему достаточно одного свежего буфера для повторного
        // запуска; ожидание четырёх буферов оставляло поток без звука навсегда.
        var requiredBuffers = _hasStarted ? 1 : StartBufferCount;
        if (!_source.Playing && _queuedCount >= requiredBuffers)
        {
            _source.StartPlaying();
            _hasStarted = true;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _source.StopPlaying();
        _source.Dispose();
    }

    private int FindFreeBuffer()
    {
        for (var i = 0; i < _queued.Length; i++)
        {
            if (!_queued[i])
                return i;
        }

        return -1;
    }
}
