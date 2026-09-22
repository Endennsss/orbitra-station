using System.Numerics;
using Content.Client.GameTicking.Managers;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Preferences;
using Robust.Client.Console;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraLobbyUiTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true };

    [Test]
    public async Task ProfileNavigationRotationAndResponsiveLayout()
    {
        var client = Pair.Client;
        var preferences = client.Resolve<IClientPreferencesManager>();
        LobbyGui lobby = null;
        await client.WaitPost(() =>
        {
            lobby = ((LobbyState) client.Resolve<IStateManager>().CurrentState).Lobby!;
            preferences.CreateCharacter(HumanoidCharacterProfile.DefaultWithSpecies("Dwarf"));
            preferences.SelectCharacter(0);
            client.Resolve<IUserInterfaceManager>().GetUIController<LobbyUIController>().ReloadCharacterSetup();
        });
        await Pair.RunTicksSync(10);
        await client.WaitAssertion(() => Assert.That(preferences.Preferences!.Characters.Count, Is.GreaterThan(1)));
        await client.WaitPost(() => client.Resolve<IClientConsoleHost>().ExecuteCommand("toggleready true"));
        await Pair.RunTicksSync(10);
        await client.WaitAssertion(() => Assert.That(client.System<ClientGameTicker>().AreWeReady, Is.True));
        await Click(lobby.CharacterPreview.FindControl<Button>("NextProfile"));
        await Pair.RunTicksSync(10);
        await client.WaitAssertion(() =>
        {
            Assert.That(preferences.Preferences!.SelectedCharacterIndex, Is.EqualTo(1));
            Assert.That(client.System<ClientGameTicker>().AreWeReady, Is.False);
        });
        await Click(lobby.CharacterPreview.FindControl<Button>("RotateProfile"));
        await client.WaitAssertion(() =>
        {
            Assert.That(preferences.Preferences!.SelectedCharacterIndex, Is.EqualTo(1));
            Assert.That(lobby.CharacterPreview.ProfilePreviewSpriteView.OverrideDirection, Is.EqualTo(Direction.East));
        });
        var chat = lobby.Chat;
        foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(3440, 1440), new Vector2(900, 1080), new Vector2(800, 600) })
        {
            await client.WaitPost(() =>
            {
                lobby.Measure(size);
                lobby.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                lobby.Measure(size);
                lobby.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
            });
            await client.WaitAssertion(() =>
            {
                Assert.That(lobby.Chat, Is.SameAs(chat));
                Assert.That(lobby.ReadyButton.GlobalPosition.Y + lobby.ReadyButton.Height,
                    Is.LessThanOrEqualTo(size.Y), "Readiness outside viewport");
                Assert.That(lobby.FindControl<Button>("InfoToggle").Visible, Is.EqualTo(lobby.FindControl<Button>("ChatToggle").Visible));
                Assert.That(lobby.CharacterPreview.GlobalPosition.X + lobby.CharacterPreview.Width / 2, Is.EqualTo(size.X / 2).Within(1));
            });
        }
        await client.WaitPost(() => lobby.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup));
        await client.WaitAssertion(() => Assert.That(lobby.CharacterSetupState.Visible, Is.True));
        await client.WaitPost(() => lobby.SwitchState(LobbyGui.LobbyGuiState.Default));
        await client.WaitAssertion(() => Assert.That(lobby.Chat, Is.SameAs(chat)));
    }

    private async Task Click(Control control)
    {
        var position = new ScreenCoordinates(control.GlobalPixelPosition + control.PixelSize / 2, control.Window?.Id ?? default);
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
        {
            var args = new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, state, position, default,
                position.Position / control.UIScale - control.GlobalPosition, position.Position - control.GlobalPixelPosition);
            await Pair.Client.DoGuiEvent(control, args);
            await Pair.RunTicksSync(1);
        }
    }
}
