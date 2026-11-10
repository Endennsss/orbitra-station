using Content.Shared._Orbitra.Atmos;
using Content.Shared.Atmos;
using NUnit.Framework;

namespace Content.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraAtmosVisualTest
{
    [TestCase(0f, 0f, 0)]
    [TestCase(25f, 0f, 1)]
    [TestCase(0f, 25f, 7)]
    [TestCase(25f, 25f, 4)]
    public void TintFollowsReleasedHeat(float plasma, float tritium, int tint)
    {
        Assert.That(OrbitraAtmosVisual.FireTint(plasma, tritium), Is.EqualTo(tint));
    }

    [TestCase(AtmosDirection.Invalid)]
    [TestCase(AtmosDirection.North)]
    [TestCase(AtmosDirection.South)]
    [TestCase(AtmosDirection.East)]
    [TestCase(AtmosDirection.West)]
    public void FireAndAirflowShareTheExistingStateByte(AtmosDirection flow)
    {
        var state = OrbitraAtmosVisual.Pack(2, 7, flow);
        Assert.Multiple(() =>
        {
            Assert.That(OrbitraAtmosVisual.FireStage(state), Is.EqualTo(2));
            Assert.That(OrbitraAtmosVisual.Tint(state), Is.EqualTo(7));
            Assert.That(OrbitraAtmosVisual.Flow(state), Is.EqualTo(flow));
            Assert.That(OrbitraAtmosVisual.FireStage(OrbitraAtmosVisual.Pack(0, 7, flow)), Is.Zero);
            Assert.That(OrbitraAtmosVisual.Tint(OrbitraAtmosVisual.Pack(0, 7, flow)), Is.Zero);
        });
    }
}
