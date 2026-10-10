using NUnit.Framework;
using Content.Server._Orbitra.VoiceChat;

namespace Content.Tests.Server._Orbitra.VoiceChat;

public sealed class OrbitraVoiceRadioRoutingTest
{
    [Test]
    public void SenderMustHaveSelectedChannelOnHeadsetOrIntrinsicRadio()
    {
        Assert.That(OrbitraVoiceRadioRouting.CanTransmit("Engineering", ["Common", "Engineering"], null), Is.True);
        Assert.That(OrbitraVoiceRadioRouting.CanTransmit("Medical", ["Common"], ["Medical"]), Is.True);
        Assert.That(OrbitraVoiceRadioRouting.CanTransmit("Security", ["Common"], ["Medical"]), Is.False);
    }

    [Test]
    public void ReceiverMatchesConfiguredChannelOrAllChannels()
    {
        Assert.That(OrbitraVoiceRadioRouting.CanReceive("Engineering", false, ["Engineering"]), Is.True);
        Assert.That(OrbitraVoiceRadioRouting.CanReceive("Medical", false, ["Engineering"]), Is.False);
        Assert.That(OrbitraVoiceRadioRouting.CanReceive("Medical", true, []), Is.True);
    }
}
