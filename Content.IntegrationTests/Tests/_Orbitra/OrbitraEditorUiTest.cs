using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Preferences;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraEditorUiTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true };

    [Test]
    public async Task ResponsiveEditorAndUnsavedTransitions()
    {
        var client = Pair.Client;
        var preferences = client.Resolve<IClientPreferencesManager>();
        var ui = client.Resolve<IUserInterfaceManager>();
        CharacterSetupGui setup = null;
        HumanoidProfileEditor editor = null;
        LobbyGui lobby = null;
        await client.WaitPost(() =>
        {
            preferences.CreateCharacter(HumanoidCharacterProfile.DefaultWithSpecies("Dwarf"));
            preferences.SelectCharacter(0);
            lobby = ((LobbyState) client.Resolve<IStateManager>().CurrentState).Lobby!;
            lobby.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup);
            setup = lobby.CharacterSetupState.Children.OfType<CharacterSetupGui>().Single();
            editor = setup.FindControl<BoxContainer>("CharEditor").Children.OfType<HumanoidProfileEditor>().Single();
        });
        await Pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            foreach (var species in new[] { "Human", "Dwarf", "Reptilian" })
            {
                editor.SetProfile(HumanoidCharacterProfile.DefaultWithSpecies(species), 0);
                foreach (var resolution in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080), new Vector2(3440, 1440), new Vector2(5120, 1440) })
                foreach (var scale in new[] { 1f, 1.25f, 1.5f })
                {
                    var size = resolution / scale;
                    var tabs = editor.FindControl<TabContainer>("TabContainer");
                    for (var tab = 0; tab < tabs.ChildCount; tab++)
                    {
                        tabs.CurrentTab = tab;
                        for (var pass = 0; pass < 5; pass++)
                        {
                            lobby.Measure(size);
                            lobby.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                        }
                        Assert.That(lobby.FindControl<PanelContainer>("OrbitraLobbyHeader").Visible, Is.False);
                        Assert.That(lobby.FindControl<Button>("ChatToggle").Visible, Is.False);
                        Assert.That(lobby.FindControl<Button>("InfoToggle").Visible, Is.False);
                        Assert.That(lobby.RightSide.Visible, Is.False);
                        var surface = setup.FindControl<PanelContainer>("BackgroundPanel");
                        Assert.That(surface.GlobalPosition.X - lobby.GlobalPosition.X + surface.Width / 2,
                            Is.EqualTo(size.X / 2).Within(1), $"Center: {resolution}/{scale}");
                        var form = editor.FindControl<BoxContainer>("OrbitraForm");
                        if (tab == 0)
                        {
                            foreach (var fieldName in new[] { "NameEdit", "SpeciesButton", "AgeEdit", "VoiceButton", "SexButton", "PronounsButton", "SpawnPriorityButton" })
                            {
                                var field = editor.FindControl<Control>(fieldName);
                                Assert.That(field.GlobalPosition.X, Is.GreaterThanOrEqualTo(form.GlobalPosition.X), fieldName);
                                Assert.That(field.GlobalPosition.X + field.Width, Is.LessThanOrEqualTo(form.GlobalPosition.X + form.Width + 1), $"{fieldName}: {resolution}/{scale}");
                                Assert.That(field.Width, Is.GreaterThan(60), fieldName);
                            }
                        }
                        var footer = editor.FindControl<BoxContainer>("OrbitraEditorFooter");
                        foreach (var action in Descendants(footer).OfType<Button>())
                            Assert.That(action.Width, Is.GreaterThan(80), $"Footer label: {action.Name}, {resolution}/{scale}");
                        Assert.That(footer.GlobalPosition.Y - setup.GlobalPosition.Y + footer.Height, Is.LessThanOrEqualTo(size.Y + 1), $"Footer: {resolution}/{scale}");
                        Assert.That(editor.FindControl<BoxContainer>("OrbitraForm").Height, Is.GreaterThan(180), $"Form: {resolution}/{scale}, tab {tab}");
                        Assert.That(editor.FindControl<PanelContainer>("OrbitraPreviewPanel").Width, Is.LessThanOrEqualTo(size.X));
                        Assert.That(setup.CloseButton.GlobalPosition.X - setup.GlobalPosition.X + setup.CloseButton.Width,
                            Is.LessThanOrEqualTo(size.X + 1), $"Close button: {resolution}/{scale}, tab {tab}");
                    }
                }
            }
            ui.GetUIController<LobbyUIController>().ReloadCharacterSetup();
            editor.Profile = editor.Profile!.WithName("Lime Unsaved Test");
            editor.IsDirty = true;
        });

        await Click(setup.FindControl<Button>("OrbitraCharacterToggle"));
        await Click(setup.FindControl<BoxContainer>("Characters").Children.OfType<CharacterPickerButton>().Last());
        await client.WaitAssertion(() => Assert.That(preferences.Preferences!.SelectedCharacterIndex, Is.EqualTo(0)));
        CharacterSetupGuiSavePanel dialog = null;
        await client.WaitAssertion(() => dialog = Descendants(ui.RootControl).OfType<CharacterSetupGuiSavePanel>().Single());
        await Click(dialog.FindControl<Button>("CancelButton"));
        await client.WaitAssertion(() =>
        {
            Assert.That(editor.IsDirty, Is.True);
            Assert.That(editor.Profile!.Name, Is.EqualTo("Lime Unsaved Test"));
        });
        await Click(setup.FindControl<Button>("OrbitraCharacterToggle"));
        await Click(setup.FindControl<BoxContainer>("Characters").Children.OfType<CharacterPickerButton>().Last());
        await client.WaitAssertion(() => dialog = Descendants(ui.RootControl).OfType<CharacterSetupGuiSavePanel>().Single());
        await Click(dialog.SaveButton);
        await Pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            Assert.That(preferences.Preferences!.SelectedCharacterIndex, Is.EqualTo(1));
            Assert.That(preferences.Preferences.Characters[0].Name, Is.EqualTo("Lime Unsaved Test"));
            editor.Profile = editor.Profile!.WithName("Discard Me");
            editor.IsDirty = true;
        });
        await Click(setup.FindControl<Button>("OrbitraCharacterToggle"));
        await Click(setup.FindControl<BoxContainer>("Characters").Children.OfType<Button>().Last());
        await client.WaitAssertion(() => dialog = Descendants(ui.RootControl).OfType<CharacterSetupGuiSavePanel>().Single());
        await Click(dialog.NoSaveButton);
        await Pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            Assert.That(preferences.Preferences!.Characters.Count, Is.EqualTo(3));
            Assert.That(preferences.Preferences.SelectedCharacterIndex, Is.EqualTo(2));
            Assert.That(preferences.Preferences.Characters[1].Name, Is.Not.EqualTo("Discard Me"));
            // Случайный профиль может быть нормализован штатным редактором; проверяем переход, а не отсутствие нормализации.
            Assert.That(editor.CharacterSlot, Is.EqualTo(2));
            Assert.That(editor.Profile!.Name, Is.Not.EqualTo("Discard Me"));
        });

        await client.WaitPost(() => editor.FindControl<TabContainer>("TabContainer").CurrentTab = 0);
        await Click(editor.FindControl<Button>("RandomizeToggle"));
        await client.WaitAssertion(() => Assert.That(editor.FindControl<BoxContainer>("RandomizePanel").Visible, Is.True));
        await Click(editor.FindControl<Button>("RandomizeToggle"));
        await client.WaitAssertion(() => Assert.That(editor.FindControl<BoxContainer>("RandomizePanel").Visible, Is.False));

        var speciesOptions = editor.FindControl<OptionButton>("SpeciesButton");
        await Click(speciesOptions);
        await client.WaitPost(() =>
        {
            // Headless-клиент не выполняет кадровую раскладку модального слоя автоматически.
            var popup = (Popup) speciesOptions.OptionsScroll.Parent!;
            Assert.That(popup.Parent, Is.Not.Null, "The open dropdown must remain attached to the modal tree.");
            popup.Measure(ui.RootControl.Size);
            popup.Arrange(UIBox2.FromDimensions(PopupContainer.GetPopupOrigin(popup), popup.DesiredSize));
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(ui.ModalRoot.ChildCount, Is.GreaterThan(0));
            foreach (var option in Descendants(speciesOptions.OptionsScroll).OfType<Button>())
            {
                Assert.That(option.HasStyleClass("OrbitraOptionRow"), Is.True, option.Text);
                Assert.That(option.Height, Is.EqualTo(32).Within(1));
            }
            var popup = (Popup) speciesOptions.OptionsScroll.Parent!;
            Assert.That(popup.Width, Is.EqualTo(speciesOptions.Width).Within(1));
            Assert.That(popup.Height, Is.LessThanOrEqualTo(288));
            Assert.That(popup.GlobalPosition.X + popup.Width, Is.LessThanOrEqualTo(ui.RootControl.Width - 15));
            Assert.That(popup.GlobalPosition.Y + popup.Height, Is.LessThanOrEqualTo(ui.RootControl.Height - 15));
        });
        await Click(Descendants(speciesOptions.OptionsScroll).OfType<Button>().First());

        await Click(setup.FindControl<Button>("OrbitraToolsToggle"));
        await client.WaitAssertion(() =>
        {
            var tools = setup.FindControl<PanelContainer>("OrbitraToolsPanel");
            Assert.That(tools.Visible, Is.True);
            Assert.That(tools.Width, Is.GreaterThan(250));
            Assert.That(tools.Height, Is.GreaterThan(100));
        });
        await Click(setup.FindControl<Button>("StatsButton"));
        await client.WaitAssertion(() =>
        {
            var stats = Descendants(ui.RootControl).OfType<Content.Client.Info.PlaytimeStats.PlaytimeStatsWindow>().Single();
            Assert.That(stats.GlobalPosition.X, Is.GreaterThanOrEqualTo(0));
            Assert.That(stats.GlobalPosition.Y + stats.Height, Is.LessThanOrEqualTo(ui.RootControl.Height + 1));
            stats.Close();
        });
        await Click(setup.FindControl<Button>("RulesButton"));
        await client.WaitAssertion(() =>
        {
            var rules = Descendants(ui.RootControl).OfType<Content.Client.Info.RulesAndInfoWindow>().Single();
            Assert.That(rules.GlobalPosition.X + rules.Width / 2, Is.EqualTo(ui.RootControl.Width / 2).Within(1));
            Assert.That(rules.GlobalPosition.Y + rules.Height, Is.LessThanOrEqualTo(ui.RootControl.Height + 1));
            rules.Close();
        });

        await client.WaitPost(() => ui.GetUIController<Content.Client.UserInterface.Systems.Guidebook.GuidebookUIController>().OpenGuidebook());
        await Pair.RunTicksSync(3);
        await client.WaitAssertion(() =>
        {
            var guide = Descendants(ui.RootControl).OfType<Content.Client.Guidebook.Controls.GuidebookWindow>().Single();
            Assert.That(guide.GlobalPosition.X, Is.GreaterThanOrEqualTo(0));
            Assert.That(guide.GlobalPosition.Y, Is.GreaterThanOrEqualTo(0));
            Assert.That(guide.GlobalPosition.Y + guide.Height, Is.LessThanOrEqualTo(ui.RootControl.Height + 1));
            guide.Close();
        });

        await client.WaitPost(() => editor.FindControl<TabContainer>("TabContainer").CurrentTab = 4);
        await Pair.RunTicksSync(3);
        var organs = editor.FindControl<Content.Client.Humanoid.MarkingPicker>("Markings").FindControl<TabContainer>("OrganTabs");
        var organSelector = organs.Parent!.Children.OfType<OptionButton>().Single();
        await client.WaitAssertion(() =>
        {
            Assert.That(organs.TabsVisible, Is.False);
            Assert.That(organSelector.ItemCount, Is.EqualTo(organs.ChildCount));
            Assert.That(organs.GetChild(organs.CurrentTab).Position.Y, Is.EqualTo(0).Within(1), "Hidden tabs retain header space");
        });
        await Click(organSelector);
        await Click(Descendants(organSelector.OptionsScroll).OfType<Button>().Skip(1).First());
        await client.WaitAssertion(() => Assert.That(organs.CurrentTab, Is.EqualTo(1)));

        await Click(setup.FindControl<Button>("OrbitraCharacterToggle"));
        var oldProfile = setup.FindControl<BoxContainer>("Characters").Children.OfType<CharacterPickerButton>().First();
        await Click(oldProfile.FindControl<Button>("DeleteButton"));
        await client.WaitAssertion(() =>
        {
            Assert.That(preferences.Preferences!.Characters.Count, Is.EqualTo(3));
            Assert.That(oldProfile.FindControl<Button>("ConfirmDeleteButton").Visible, Is.True);
        });
        await Click(oldProfile.FindControl<Button>("ConfirmDeleteButton"));
        await Pair.RunTicksSync(3);
        await client.WaitAssertion(() => Assert.That(preferences.Preferences!.Characters.Count, Is.EqualTo(2)));
    }

    [Test]
    public async Task EntryWindowsPreservePositionAndDoNotStyleUnrelatedWindows()
    {
        var client = Pair.Client;
        var ui = client.Resolve<IUserInterfaceManager>();
        Content.Client.Options.UI.OptionsMenu options = null;
        await client.WaitPost(() =>
        {
            options = new Content.Client.Options.UI.OptionsMenu();
            options.OpenCentered();
        });
        await Pair.RunTicksSync(3);
        await client.WaitPost(() =>
        {
            LayoutContainer.SetPosition(options, new Vector2(16, 16));
            options.Tabs.CurrentTab = 1;
            Assert.That(options.FindControl<OptionButton>("OrbitraTabSelect").SelectedId, Is.EqualTo(1));
        });
        await Pair.RunTicksSync(3);
        await client.WaitAssertion(() =>
        {
            Assert.That(options.Position.X, Is.EqualTo(16).Within(1));
            Assert.That(options.Position.Y, Is.EqualTo(16).Within(1));
            options.Close();
            options.OpenCentered();
        });
        await Pair.RunTicksSync(3);
        await client.WaitAssertion(() =>
        {
            Assert.That(options.Position.X, Is.EqualTo(16).Within(1));
            Assert.That(options.Position.Y, Is.EqualTo(16).Within(1));
            using var unrelated = new Robust.Client.UserInterface.CustomControls.DefaultWindow();
            unrelated.OpenCentered();
            Assert.That(unrelated.HasStyleClass("OrbitraEntryWindow"), Is.False);
            unrelated.Close();
            options.Close();
            options.Dispose();
        });
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
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
