using System;
using System.Numerics;
using Content.Shared._Orbitra.VoiceChat;
using NUnit.Framework;
using Robust.Shared.Map;

namespace Content.Tests.Shared._Orbitra.VoiceChat;

[TestFixture]
public sealed class OrbitraVoiceChatPolicyTest
{
    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(1500, true)]
    [TestCase(1501, false)]
    public void PayloadBounds(int length, bool expected)
    {
        Assert.That(OrbitraVoiceChatPolicy.IsValidPayloadLength(length), Is.EqualTo(expected));
    }

    [Test]
    public void SequenceAllowsGapsAndWraps()
    {
        Assert.That(OrbitraVoiceChatPolicy.IsSequenceNewer(4, 5), Is.True);
        Assert.That(OrbitraVoiceChatPolicy.IsSequenceNewer(4, 9), Is.True);
        Assert.That(OrbitraVoiceChatPolicy.IsSequenceNewer(ushort.MaxValue, 0), Is.True);
        Assert.That(OrbitraVoiceChatPolicy.IsSequenceNewer(9, 9), Is.False);
        Assert.That(OrbitraVoiceChatPolicy.IsSequenceNewer(9, 8), Is.False);
    }

    [Test]
    public void ProximityRequiresTheSameMapAndStrictRange()
    {
        var origin = new MapCoordinates(Vector2.Zero, new MapId(1));

        Assert.That(OrbitraVoiceChatPolicy.IsWithinProximity(origin,
            new MapCoordinates(6.9f, 0f, new MapId(1))), Is.True);
        Assert.That(OrbitraVoiceChatPolicy.IsWithinProximity(origin,
            new MapCoordinates(7f, 0f, new MapId(1))), Is.False);
        Assert.That(OrbitraVoiceChatPolicy.IsWithinProximity(origin,
            new MapCoordinates(1f, 0f, new MapId(2))), Is.False);
    }

    [Test]
    public void InputGainClampsSamples()
    {
        var samples = new short[] { 10_000, -20_000, short.MaxValue };

        OrbitraVoiceChatPolicy.ApplyInputGain(samples, 2f);

        Assert.That(samples, Is.EqualTo(new short[] { 20_000, short.MinValue, short.MaxValue }));
    }

    [Test]
    public void LowFpsBurstIsAcceptedWithoutAllowingAnUnboundedBurst()
    {
        var limiter = new OrbitraVoiceChatRateLimiter();

        Assert.That(limiter.TryAccept(TimeSpan.Zero), Is.True);
        Assert.That(limiter.TryAccept(TimeSpan.FromMilliseconds(10)), Is.True);
        Assert.That(limiter.TryAccept(TimeSpan.FromMilliseconds(20)), Is.True);

        var burstAccepted = 0;
        for (var i = 0; i < 100; i++)
        {
            if (limiter.TryAccept(TimeSpan.FromMilliseconds(20)))
                burstAccepted++;
        }

        Assert.That(burstAccepted, Is.EqualTo(4));
    }

    [Test]
    public void EncodedFrameEstimateIncludesPayload()
    {
        var frame = new MsgOrbitraVoiceFrame
        {
            Sequence = 42,
            Data = new byte[] { 1, 2, 3 }
        };

        Assert.That(frame.EstimateBufferSize(), Is.GreaterThanOrEqualTo(frame.Data.Length + 16));
        Assert.That(frame.DeliveryMethod, Is.EqualTo(Lidgren.Network.NetDeliveryMethod.UnreliableSequenced));
        Assert.That(frame.SequenceChannel, Is.EqualTo(1));
    }

    [Test]
    public void ProximityTransmissionDoesNotRequireChannel()
    {
        Assert.That(OrbitraVoiceChatPolicy.IsValidTransmission(
            OrbitraVoiceTransmissionMode.Proximity,
            string.Empty), Is.True);
    }

    [Test]
    public void RadioTransmissionRequiresBoundedChannelId()
    {
        Assert.That(OrbitraVoiceChatPolicy.IsValidTransmission(
            OrbitraVoiceTransmissionMode.Radio,
            "Security"), Is.True);
        Assert.That(OrbitraVoiceChatPolicy.IsValidTransmission(
            OrbitraVoiceTransmissionMode.Radio,
            string.Empty), Is.False);
        Assert.That(OrbitraVoiceChatPolicy.IsValidTransmission(
            OrbitraVoiceTransmissionMode.Radio,
            new string('x', OrbitraVoiceChatPolicy.MaxRadioChannelIdLength + 1)), Is.False);
    }

    [Test]
    public void UnknownTransmissionModeFallsBackToInvalid()
    {
        Assert.That(OrbitraVoiceChatPolicy.IsKnownTransmissionMode((OrbitraVoiceTransmissionMode) 99), Is.False);
    }
}
