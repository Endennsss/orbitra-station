using Content.Client.PDA;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraPdaColorsTest : GameTest
{
    [Test]
    public async Task CaseColorsFollowThePdaAndMissingAccentsAreHidden()
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var window = new PdaWindow();
            try
            {
                foreach (var color in new[] { "#C04030", "#3070C0" })
                {
                    window.BorderColor = color;
                    window.AccentHColor = color;
                    window.AccentVColor = color;
                    Assert.That(window.Background.ActualModulateSelf, Is.EqualTo(Color.FromHex(color)));
                    Assert.That(window.AccentH.ActualModulateSelf, Is.EqualTo(Color.FromHex(color)));
                    Assert.That(window.AccentV.ActualModulateSelf, Is.EqualTo(Color.FromHex(color)));
                    Assert.That(window.AccentH.Visible && window.AccentV.Visible, Is.True);
                }

                window.BorderColor = null;
                window.AccentHColor = null;
                window.AccentVColor = null;
                Assert.That(window.Background.ActualModulateSelf, Is.EqualTo(Color.White));
                Assert.That(window.AccentH.Visible || window.AccentV.Visible, Is.False);
            }
            finally
            {
                window.Orphan();
            }
        });
    }
}
