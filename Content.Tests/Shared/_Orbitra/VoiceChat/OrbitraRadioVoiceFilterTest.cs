using NUnit.Framework;
using Content.Shared._Orbitra.VoiceChat;

namespace Content.Tests.Shared._Orbitra.VoiceChat;

public sealed class OrbitraRadioVoiceFilterTest
{
    [Test]
    public void FilterIsDeterministicAndKeepsSamplesBounded()
    {
        var first = new short[] { -32000, -12000, 0, 12000, 32000 };
        var second = (short[]) first.Clone();
        uint firstState = 1;
        uint secondState = 1;

        OrbitraRadioVoiceFilter.Apply(first, ref firstState);
        OrbitraRadioVoiceFilter.Apply(second, ref secondState);

        Assert.That(first, Is.EqualTo(second));
        Assert.That(firstState, Is.Not.EqualTo(1u));
        Assert.That(first, Has.All.InRange(short.MinValue, short.MaxValue));
        Assert.That(first, Is.Not.EqualTo(new short[] { -32000, -12000, 0, 12000, 32000 }));
    }
}
