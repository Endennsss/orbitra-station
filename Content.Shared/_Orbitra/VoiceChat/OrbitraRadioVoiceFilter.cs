using System;

namespace Content.Shared._Orbitra.VoiceChat;

/// <summary>
/// Детерминированное ухудшение PCM до тембра узкополосной рации без постоянного белого шума.
/// </summary>
public static class OrbitraRadioVoiceFilter
{
    public static void Apply(Span<short> samples, ref uint state)
    {
        if (state == 0)
            state = 0x6D2B79F5;

        var smoothed = 0f;
        for (var i = 0; i < samples.Length; i++)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            // Сглаживание убирает верхние частоты, а компрессия и разрядность создают
            // характерный «телефонный» тембр без добавления шипения в каждый сэмпл.
            smoothed = smoothed * 0.28f + samples[i] * 0.72f;
            var normalized = Math.Clamp(smoothed / short.MaxValue, -1f, 1f);
            normalized = MathF.Tanh(normalized * 1.35f) / MathF.Tanh(1.35f);
            var flutter = 0.96f + MathF.Sin(state * 0.00000008f) * 0.035f;
            var sample = normalized * short.MaxValue * flutter;
            sample = Math.Clamp(sample, short.MinValue, short.MaxValue);
            samples[i] = (short) (MathF.Round(sample / 512f) * 512f);
        }
    }
}
