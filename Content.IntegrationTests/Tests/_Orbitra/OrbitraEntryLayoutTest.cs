using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._Orbitra.Lobby;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Lobby.UI.Roles;
using Content.Client.Lobby.UI.ProfileEditorControls;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraEntryLayoutTest : GameTest
{
    // Профили и состояние анимации должны начинаться с чистого клиента, без предыдущего редактора.
    public override PoolSettings PoolSettings => new() { InLobby = true, Fresh = true, Destructive = true, Dirty = true };

    [Test]
    public async Task BottomDockAndAlignedFieldsAtAllSupportedSizes()
    {
        var client = Pair.Client;
        LobbyGui lobby = null;
        await client.WaitPost(() => lobby = ((LobbyState) client.Resolve<IStateManager>().CurrentState).Lobby!);
        foreach (var resolution in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080), new Vector2(3440, 1440), new Vector2(5120, 1440) })
        foreach (var scale in new[] { 1f, 1.25f, 1.5f })
        {
            var size = resolution / scale;
            await client.WaitPost(() =>
            {
                lobby.SwitchState(LobbyGui.LobbyGuiState.Default);
                Layout(lobby, size);
            });
            await client.WaitAssertion(() =>
            {
                var column = lobby.FindControl<BoxContainer>("LeftSide");
                var dock = lobby.FindControl<PanelContainer>("ReadyCard");
                Assert.That(dock.GlobalPosition.Y + dock.Height, Is.EqualTo(column.GlobalPosition.Y + column.Height).Within(1), $"Dock {resolution}/{scale}");
                Assert.That(dock.Width, Is.LessThanOrEqualTo(480));
                Assert.That(dock.GlobalPosition.X + dock.Width / 2, Is.EqualTo(size.X / 2).Within(1), "Round actions must share the screen axis");
                var card = lobby.CharacterPreview.FindControl<PanelContainer>("CrewCard");
                Assert.That(card.GlobalPosition.X + card.Width / 2, Is.EqualTo(size.X / 2).Within(1), "Crew card must share the screen axis");
                Assert.That(lobby.ReadyButton.Height, Is.EqualTo(44).Within(1));
                Assert.That(lobby.ObserveButton.Height, Is.EqualTo(44).Within(1));
                Assert.That(lobby.CharacterPreview.GlobalPosition.Y + lobby.CharacterPreview.Height, Is.LessThanOrEqualTo(dock.GlobalPosition.Y));
                Assert.That(dock.GlobalPosition.Y + dock.Height, Is.LessThanOrEqualTo(size.Y));
                var credits = lobby.FindControl<BoxContainer>("Credits");
                Assert.That(credits.GlobalPosition.X, Is.EqualTo(16).Within(1));
                Assert.That(credits.GlobalPosition.X + credits.Width, Is.EqualTo(size.X - 16).Within(1));
                Assert.That(dock.GlobalPosition.Y + dock.Height, Is.LessThanOrEqualTo(credits.GlobalPosition.Y));
                if (lobby.FindControl<BoxContainer>("InfoDock").Visible)
                {
                    var info = lobby.FindControl<PanelContainer>("InfoPanel");
                    Assert.That(info.Height, Is.GreaterThan(100), $"Information panel must not collapse: {resolution}/{scale}, parent={info.Parent?.Name}, visible={info.VisibleInTree}, desired={info.DesiredSize}");
                    Assert.That(info.GlobalPosition.Y + info.Height, Is.LessThanOrEqualTo(credits.GlobalPosition.Y));
                    var header = lobby.FindControl<PanelContainer>("OrbitraLobbyHeader");
                    Assert.That(info.GlobalPosition.X, Is.EqualTo(header.GlobalPosition.X).Within(1));
                    var about = lobby.FindControl<Button>("AboutButton");
                    foreach (var button in Descendants(info).OfType<Button>().Where(button => button.VisibleInTree))
                    {
                        Assert.That(button.Width, Is.EqualTo(about.Width).Within(1), button.Text);
                    }
                }
                foreach (var width in new[] { 360f, 480f })
                foreach (var chatVisible in new[] { false, true })
                {
                    lobby.SetOrbitraChatWidth(width);
                    lobby.RightSide.Visible = chatVisible;
                    Layout(lobby, size);
                    Assert.That(card.GlobalPosition.X + card.Width / 2, Is.EqualTo(size.X / 2).Within(1), "Chat must not move the crew axis");
                }
            });
            await client.WaitPost(() =>
            {
                lobby.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup);
                Layout(lobby, size);
            });
            await client.WaitAssertion(() =>
            {
                var fields = new[] { "AgeEdit", "SexButton", "VoiceButton", "PronounsButton", "SpawnPriorityButton" }
                    .Select(name => Descendants(lobby.CharacterSetupState).Single(control => control.Name == name)).ToArray();
                foreach (var field in fields)
                {
                    Assert.That(field.GlobalPosition.X, Is.EqualTo(fields[0].GlobalPosition.X).Within(1), $"Left {field.Name} {size}");
                    Assert.That(field.GlobalPosition.X + field.Width, Is.EqualTo(fields[0].GlobalPosition.X + fields[0].Width).Within(1), $"Right {field.Name} {size}");
                }
            });
            await client.WaitPost(() => OrbitraWindowLifecycleTest.Frame(client.Resolve<IUserInterfaceManager>(), 0));
        }
    }

    [Test]
    public async Task CompactPriorityKeepsSelection()
    {
        var client = Pair.Client;
        RequirementsSelector priority = null;
        await client.WaitPost(() =>
        {
            priority = new RequirementsSelector();
            priority.Setup(new[] { ("humanoid-profile-editor-job-priority-never-button", 0), ("humanoid-profile-editor-job-priority-low-button", 1), ("humanoid-profile-editor-job-priority-medium-button", 2), ("humanoid-profile-editor-job-priority-high-button", 3) }, "Captain", 200, null);
            priority.Select(2);
            priority.ApplyOrbitraLayout();
            var root = client.Resolve<IUserInterfaceManager>().StateRoot;
            root.AddChild(priority);
        });
        foreach (var width in new[] { 800, 400, 520, 519, 1600, 400 })
        {
            await client.WaitPost(() =>
            {
                Layout(priority, new Vector2(width, 200));
            });
            await client.WaitAssertion(() =>
            {
                Assert.That(priority.Selected, Is.EqualTo(2));
                Assert.That(Descendants(priority).OfType<OptionButton>().Single().Visible, Is.EqualTo(width < 520));
            });
        }
        await client.WaitPost(priority.Dispose);
    }

    [Test]
    public async Task ProfileTransitionClearsOutgoingDummyAndHonorsReducedMotion()
    {
        var client = Pair.Client;
        LobbyCharacterPreviewPanel preview = null;
        PlayerPreferences prefs = null;
        Robust.Shared.GameObjects.EntityUid outgoing = default;
        await client.WaitPost(() =>
        {
            var manager = client.Resolve<IClientPreferencesManager>();
            manager.CreateCharacter(HumanoidCharacterProfile.DefaultWithSpecies("Dwarf"));
            manager.SelectCharacter(0);
            preview = ((LobbyState) client.Resolve<IStateManager>().CurrentState).Lobby!.CharacterPreview;
            prefs = manager.Preferences!;
            client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, false);
            preview.RefreshOrbitraProfile((HumanoidCharacterProfile) prefs.Characters[0], prefs);
            outgoing = preview.ProfilePreviewSpriteView.PreviewDummy;
        });
        await client.WaitPost(() =>
        {
            client.Resolve<IClientPreferencesManager>().SelectCharacter(1);
            prefs = client.Resolve<IClientPreferencesManager>().Preferences!;
            preview.RefreshOrbitraProfile((HumanoidCharacterProfile) prefs.Characters[1], prefs);
        });
        await client.WaitAssertion(() =>
        {
            var old = preview.FindControl<ProfilePreviewSpriteView>("OutgoingPreview");
            Assert.That(old.Visible, Is.True);
            Assert.That(old.PreviewDummy, Is.EqualTo(outgoing), "Outgoing mannequin must be retained, not respawned");
            preview.RefreshOrbitraProfile((HumanoidCharacterProfile) prefs.Characters[1], prefs);
            Assert.That(old.Visible, Is.True, "Duplicate notification must not end the transition");
            Assert.That(old.PreviewDummy, Is.EqualTo(outgoing));
        });
        await client.WaitPost(() => preview.AdvanceOrbitraTransition(0.12f));
        await client.WaitAssertion(() =>
        {
            var stage = preview.FindControl<OrbitraPreviewStage>("PreviewStage");
            Assert.That(stage.IncomingOffset, Is.InRange(0f, 48f));
            Assert.That(stage.OutgoingOffset, Is.InRange(-48f, 0f));
        });
        await client.WaitPost(() => preview.AdvanceOrbitraTransition(0.24f));
        await client.WaitAssertion(() =>
        {
            Assert.That(preview.FindControl<ProfilePreviewSpriteView>("OutgoingPreview").PreviewDummy.IsValid(), Is.False);
            Assert.That(preview.FindControl<Button>("NextProfile").Disabled, Is.False);
        });
        await client.WaitPost(() =>
        {
            client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            client.Resolve<IClientPreferencesManager>().SelectCharacter(0);
            prefs = client.Resolve<IClientPreferencesManager>().Preferences!;
            preview.RefreshOrbitraProfile((HumanoidCharacterProfile) prefs.Characters[0], prefs);
        });
        await client.WaitAssertion(() => Assert.That(preview.FindControl<ProfilePreviewSpriteView>("OutgoingPreview").Visible, Is.False));
        await client.WaitPost(() =>
        {
            client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, false);
            preview.RefreshOrbitraProfile((HumanoidCharacterProfile) prefs.Characters[0], prefs);
        });
        await client.WaitAssertion(() => Assert.That(preview.FindControl<ProfilePreviewSpriteView>("OutgoingPreview").Visible, Is.False, "Same-slot refresh must not animate"));
        await client.WaitPost(() => preview.RequestOrbitraProfile(-1));
        await client.WaitAssertion(() =>
        {
            Assert.That(client.Resolve<IClientPreferencesManager>().Preferences!.SelectedCharacterIndex, Is.EqualTo(1), "Cyclic previous slot");
            Assert.That(preview.FindControl<OrbitraPreviewStage>("PreviewStage").IncomingOffset, Is.EqualTo(-48));
        });
        await client.WaitPost(() => preview.RequestOrbitraProfile(-1));
        await client.WaitAssertion(() => Assert.That(client.Resolve<IClientPreferencesManager>().Preferences!.SelectedCharacterIndex, Is.EqualTo(1), "Rapid clicks ignored during transition"));
        await client.WaitPost(() => preview.AdvanceOrbitraTransition(0.24f));
        await client.WaitPost(preview.ClearOrbitraPreview);
        await client.WaitAssertion(() => Assert.That(preview.ProfilePreviewSpriteView.PreviewDummy.IsValid(), Is.False));
    }

    private static void Layout(Control control, Vector2 size)
    {
        for (var pass = 0; pass < 5; pass++)
        {
            // Ручная раскладка не обрабатывает очередь UI: после переноса панели обновляем и дочерние измерения.
            foreach (var child in Descendants(control))
                child.InvalidateMeasure();
            control.InvalidateMeasure();
            control.Measure(size);
            control.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
        }
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }
}
