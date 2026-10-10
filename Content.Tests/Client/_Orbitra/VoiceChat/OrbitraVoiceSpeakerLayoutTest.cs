using System.Numerics;
using NUnit.Framework;
using Content.Client._Orbitra.VoiceChat;

namespace Content.Tests.Client._Orbitra.VoiceChat;

public sealed class OrbitraVoiceSpeakerLayoutTest
{
    [Test]
    public void CardStaysInsideActiveViewportRightEdge()
    {
        var rect = OrbitraVoiceSpeakerLayout.CalculateCardRect(new Vector2(1200, 700), 820, 0, 1f);

        Assert.That(rect.Right, Is.LessThanOrEqualTo(820));
        Assert.That(rect.Left, Is.GreaterThanOrEqualTo(0));
        Assert.That(rect.Bottom, Is.LessThanOrEqualTo(700));
    }

    [Test]
    public void FullHudUsesScreenRightEdge()
    {
        var rect = OrbitraVoiceSpeakerLayout.CalculateCardRect(new Vector2(1200, 700), 1200, 0, 1f);

        Assert.That(rect.Right, Is.EqualTo(1184).Within(0.01));
    }
}
