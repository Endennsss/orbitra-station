using Content.Client._Orbitra.Stylesheets;
using Content.Client.PDA;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraPdaColorsTest : GameTest
{
    [Test]
    public async Task CaseColorFollowsThePda()
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var window = new PdaWindow();
            try
            {
                foreach (var color in new[] { "#C04030", "#3070C0" })
                {
                    window.BorderColor = color;
                    var baseColor = Color.FromHex(color);
                    var shell = Color.InterpolateBetween(OrbitraPalettes.PanelHighlight, baseColor, 0.54f);
                    Assert.That(window.Background.ActualModulateSelf, Is.EqualTo(shell));
                }

                window.BorderColor = null;
                Assert.That(window.Background.ActualModulateSelf, Is.EqualTo(OrbitraPalettes.PanelHighlight));
            }
            finally
            {
                window.Orphan();
            }
        });
    }
}
