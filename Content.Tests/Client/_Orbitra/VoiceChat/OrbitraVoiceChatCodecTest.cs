using Content.Client._Orbitra.VoiceChat;
using Content.Shared._Orbitra.VoiceChat;
using NUnit.Framework;

namespace Content.Tests.Client._Orbitra.VoiceChat;

[TestFixture]
public sealed class OrbitraVoiceChatCodecTest
{
    [Test]
    public void OpusRoundTripProducesAValidFrame()
    {
        var samples = new short[OrbitraVoiceChatPolicy.SamplesPerFrame];
        samples[0] = short.MaxValue;
        var encoded = new byte[OrbitraVoiceChatPolicy.MaxEncodedFrameBytes];

        using var encoder = new OrbitraVoiceChatEncoder();
        var encodedLength = encoder.Encode(samples, encoded);

        Assert.That(OrbitraVoiceChatPolicy.IsValidPayloadLength(encodedLength), Is.True);

        var payload = encoded[..encodedLength];
        var decoded = new short[OrbitraVoiceChatPolicy.SamplesPerFrame * 6];
        using var decoder = new OrbitraVoiceChatDecoder();
        var decodedSamples = decoder.Decode(payload, decoded);

        Assert.That(decodedSamples, Is.GreaterThan(0));
        Assert.That(decodedSamples, Is.LessThanOrEqualTo(decoded.Length));
    }
}
