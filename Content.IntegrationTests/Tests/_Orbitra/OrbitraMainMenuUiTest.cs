using System.Numerics;
using Content.Client.MainMenu.UI;
using Content.IntegrationTests.Fixtures;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraMainMenuUiTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true };

    [Test]
    public async Task ConnectingDoesNotShrinkMenuOrFields()
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var ui = Pair.Client.Resolve<IUserInterfaceManager>();
            using var menu = new MainMenuControl(Pair.Client.Resolve<IResourceCache>(), Pair.Client.Resolve<IConfigurationManager>());
            ui.StateRoot.AddChild(menu);
            menu.OrbitraDevLobbyButton.Visible = true;
            foreach (var resolution in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(3440, 1440) })
            foreach (var scale in new[] { 1f, 1.25f, 1.5f })
            {
                var size = resolution / scale;
                void Layout()
                {
                    for (var i = 0; i < 4; i++)
                    {
                        menu.Measure(size);
                        menu.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                    }
                }
                menu.OrbitraDevLobbyButton.Text = "Подключиться напрямую + лобби";
                menu.OrbitraDevLobbyButton.Disabled = false;
                menu.DirectConnectButton.Disabled = false;
                Layout();
                var box = menu.FindControl<BoxContainer>("VBox");
                var initial = box.Width;
                foreach (var text in new[] { "Подготовка dev-лобби…", "Подключиться напрямую + лобби" })
                {
                    menu.OrbitraDevLobbyButton.Text = text;
                    menu.OrbitraDevLobbyButton.Disabled = !menu.OrbitraDevLobbyButton.Disabled;
                    menu.DirectConnectButton.Disabled = menu.OrbitraDevLobbyButton.Disabled;
                    Layout();
                    Assert.That(box.Width, Is.EqualTo(initial).Within(1));
                    Assert.That(menu.UsernameBox.Width, Is.GreaterThan(240));
                    Assert.That(menu.AddressBox.Width, Is.EqualTo(menu.UsernameBox.Width).Within(1));
                    Assert.That(box.GlobalPosition.X + box.Width / 2, Is.EqualTo(size.X / 2).Within(1));
                    var scroll = menu.FindControl<ScrollContainer>("OrbitraMenuScroll");
                    Assert.That(scroll.Height, Is.LessThanOrEqualTo(size.Y + 1));
                }
            }
        });
    }
}
