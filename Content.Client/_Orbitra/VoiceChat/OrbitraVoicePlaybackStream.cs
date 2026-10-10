using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._Orbitra.VoiceChat;
using Robust.Client.Audio;
using Robust.Shared.Audio.Sources;

namespace Content.Client._Orbitra.VoiceChat;

/// <summary>
/// Feeds short PCM chunks to the public audio API. Several frames are grouped
/// into one source to avoid creating an OpenAL source for every network packet.
/// </summary>
internal sealed class OrbitraVoicePlaybackStream : IDisposable
{
    private const int FramesPerChunk = 4;
    private const int MaxPendingChunks = 12;
    private const int SamplesPerChunk = OrbitraVoiceChatPolicy.SamplesPerFrame * FramesPerChunk;

    private readonly IAudioManager _audio;
    private readonly Queue<short[]> _pending = new();
    private readonly short[] _chunk = new short[SamplesPerChunk];
    private int _chunkSamples;
    private ActiveChunk? _active;
    private Vector2 _position;
    private float _gain;
    private bool _radio;
    private bool _disposed;

    private sealed class ActiveChunk
    {
        public required AudioStream Stream;
        public required IAudioSource Source;
    }

    public OrbitraVoicePlaybackStream(IAudioManager audio, Vector2 position, float gain, bool radio = false)
    {
        _audio = audio;
        _position = position;
        _gain = gain;
        _radio = radio;
    }

    public void SetPosition(Vector2 position)
    {
        _position = position;
        if (_active is { } active)
            active.Source.Position = position;
    }

    public void SetGain(float gain)
    {
        _gain = gain;
        if (_active is { } active)
            active.Source.Gain = gain;
    }

    public void SetRadioMode(bool radio)
    {
        _radio = radio;
        if (_active is { } active)
            ApplySourceSettings(active.Source);
    }

    public void Enqueue(ReadOnlySpan<short> samples)
    {
        if (_disposed)
            return;

        while (!samples.IsEmpty)
        {
            var copyCount = Math.Min(samples.Length, _chunk.Length - _chunkSamples);
            samples[..copyCount].CopyTo(_chunk.AsSpan(_chunkSamples));
            _chunkSamples += copyCount;
            samples = samples[copyCount..];

            if (_chunkSamples != _chunk.Length)
                continue;

            QueueChunk(_chunk);
            _chunkSamples = 0;
        }

        Pump();
    }

    public void Pump()
    {
        if (_disposed)
            return;

        if (_active is { } active && !active.Source.Playing)
        {
            DisposeActive(active);
            _active = null;
        }

        if (_active != null)
            return;

        if (_pending.Count > 0)
        {
            StartChunk(_pending.Dequeue());
            return;
        }

        // Flush a short tail after a speaker stops sending frames.
        if (_chunkSamples > 0)
        {
            var tail = new short[_chunkSamples];
            _chunk.AsSpan(0, _chunkSamples).CopyTo(tail);
            _chunkSamples = 0;
            StartChunk(tail);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_active is { } active)
            DisposeActive(active);

        _active = null;
        _pending.Clear();
        _chunkSamples = 0;
    }

    private void QueueChunk(ReadOnlySpan<short> samples)
    {
        if (_pending.Count == MaxPendingChunks)
            _pending.Dequeue();

        var copy = new short[samples.Length];
        samples.CopyTo(copy);
        _pending.Enqueue(copy);
    }

    private void StartChunk(short[] samples)
    {
        AudioStream? stream = null;
        IAudioSource? source = null;
        try
        {
            stream = _audio.LoadAudioRaw(samples, 1, 48_000, "Orbitra voice");
            source = _audio.CreateAudioSource(stream);
            if (source == null)
            {
                stream.Dispose();
                return;
            }

            ApplySourceSettings(source);
            source.StartPlaying();
            _active = new ActiveChunk { Stream = stream, Source = source };
        }
        catch
        {
            source?.Dispose();
            stream?.Dispose();
            _active = null;
        }
    }

    private void ApplySourceSettings(IAudioSource source)
    {
        source.Position = _position;
        source.Gain = _gain;
        source.Global = _radio;
        source.MaxDistance = _radio ? 1f : OrbitraVoiceChatPolicy.ProximityRange;
        source.ReferenceDistance = 1f;
        source.RolloffFactor = 1f;
    }

    private static void DisposeActive(ActiveChunk active)
    {
        active.Source.StopPlaying();
        active.Source.Dispose();
        active.Stream.Dispose();
    }
}
