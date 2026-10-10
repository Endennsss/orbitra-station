using System;
using System.Numerics;

namespace Content.Client._Orbitra.VoiceChat;

public readonly record struct OrbitraVoiceSpeakerCardRect(float Left, float Top, float Width, float Height)
{
    public float Right => Left + Width;
    public float Bottom => Top + Height;
}

public static class OrbitraVoiceSpeakerLayout
{
    public static OrbitraVoiceSpeakerCardRect CalculateCardRect(Vector2 canvasSize, float activeRightEdge, int index, float uiScale)
    {
        uiScale = Math.Max(1f, uiScale);
        var margin = 16f * uiScale;
        var width = MathF.Min(240f * uiScale, MathF.Max(0f, activeRightEdge - margin));
        var height = 46f * uiScale;
        var gap = 6f * uiScale;
        var right = Math.Clamp(activeRightEdge - margin, width, canvasSize.X - margin);
        var bottom = canvasSize.Y - margin - index * (height + gap);
        return new OrbitraVoiceSpeakerCardRect(right - width, bottom - height, width, height);
    }
}
