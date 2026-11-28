using Content.Client._Orbitra.Lavaland;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Orbitra.Lavaland;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed class OrbitraLavalandTerminalTest : GameTest
{
    [Test]
    public async Task OnlyAnAvailableRouteCanBeRequested()
    {
        await Client.WaitAssertion(() =>
        {
            using var window = new OrbitraLavalandWindow();
            window.Display(new LavalandConsoleState(LavalandFerryStatus.Boarding, LavalandSide.Station,
                LavalandSide.Outpost, TimeSpan.FromSeconds(10), false), LavalandSide.Outpost);
            Assert.That(window.RequestButton.Disabled, Is.True);
            Assert.That(window.Countdown.Visible, Is.True);

            window.Display(new LavalandConsoleState(LavalandFerryStatus.Docked, LavalandSide.Station,
                LavalandSide.Outpost, null, true), LavalandSide.Outpost);
            Assert.That(window.RequestButton.Disabled, Is.False);
            Assert.That(window.Countdown.Visible, Is.False);
            var recallLabel = window.RequestButton.Text;
            window.Display(new LavalandConsoleState(LavalandFerryStatus.Docked, LavalandSide.Station,
                LavalandSide.Outpost, null, true), LavalandSide.Station);
            Assert.That(window.RequestButton.Text, Is.Not.EqualTo(recallLabel));
        });
    }
}
