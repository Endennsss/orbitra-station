using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client.Lobby;
using Content.Client.Voting.UI;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared.Voting;
using Robust.Client.Input;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Content.Client.RoundEnd;
using Content.Client.UserInterface.Systems.Ghost.Controls.Roles;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Client._Orbitra.UserInterface;
using Content.IntegrationTests.Fixtures;
using Content.Server.Preferences.Managers;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Roles;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraRoundMenusTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true, NoLoadTestPrototypes = true };

    private static IEnumerable<Control> All(Control root)
    {
        yield return root;
        foreach (var child in root.Children)
        foreach (var nested in All(child))
            yield return nested;
    }

    [Test]
    public async Task VoteCallMenuKeepsFooterAndControlsInsideWindow()
    {
        VoteCallMenu menu = null!;
        await Client.WaitPost(() =>
        {
            menu = new VoteCallMenu();
            menu.OpenCentered();
        });

        try
        {
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() =>
            {
                var footer = All(menu).OfType<Label>().Single(label => label.Text?.Contains("Robust", StringComparison.Ordinal) == true);
                var selector = menu.FindControl<OptionButton>("VoteTypeButton");
                var create = menu.FindControl<Button>("CreateButton");

                Assert.That(menu.Width, Is.GreaterThanOrEqualTo(440));
                Assert.That(menu.Height, Is.GreaterThanOrEqualTo(280));
                Assert.That(selector.GlobalPixelPosition.Y + selector.PixelHeight,
                    Is.LessThanOrEqualTo(menu.GlobalPixelPosition.Y + menu.PixelHeight + 1));
                Assert.That(create.GlobalPixelPosition.Y + create.PixelHeight,
                    Is.LessThanOrEqualTo(menu.GlobalPixelPosition.Y + menu.PixelHeight + 1));
                Assert.That(footer.VisibleInTree, Is.True);
                Assert.That(footer.GlobalPixelPosition.Y + footer.PixelHeight,
                    Is.LessThanOrEqualTo(menu.GlobalPixelPosition.Y + menu.PixelHeight + 1));
            });
        }
        finally
        {
            await Client.WaitPost(() => menu.Dispose());
        }
    }

    [Test]
    public async Task VoteCallMenuRefreshesTargetsAfterTheTypeWasSelected()
    {
        VoteCallMenu menu = null!;
        const string target = "Длинное имя игрока с названием профессии";
        await Client.WaitPost(() =>
        {
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            menu = new VoteCallMenu();
            menu.OpenCentered();
        });

        try
        {
            await Pair.RunTicksSync(6);
            await Client.WaitPost(() =>
            {
                var selector = menu.FindControl<OptionButton>("VoteTypeButton");
                selector.GrabKeyboardFocus();
                Key(Keyboard.Key.Return);
                Key(Keyboard.Key.End);
                Key(Keyboard.Key.Return);
                Assert.That(selector.SelectedId, Is.EqualTo((int) StandardVoteType.Votekick));
                Assert.That(menu.FindControl<Button>("CreateButton").Disabled, Is.True);
            });

            await Server.WaitPost(() => Server.Resolve<IEntityNetworkManager>().SendSystemNetworkMessage(
                new VotePlayerListResponseEvent([(new NetUserId(Guid.NewGuid()), NetEntity.Invalid, target)], false),
                ServerSession!.Channel));
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() =>
            {
                var options = menu.FindControl<BoxContainer>("VoteOptionsButtonContainer").Children.OfType<OrbitraOptionButton>().ToArray();
                Assert.That(options, Has.Length.EqualTo(2));
                Assert.That(All(options[0]).OfType<Label>().Any(label => label.Text?.Trim() == target), Is.True);
                Assert.That(options[1].ItemCount, Is.GreaterThanOrEqualTo(3));
            });
        }
        finally
        {
            await Client.WaitPost(() => menu.Dispose());
        }
    }

    [Test]
    public async Task LobbyVoteKeepsManyOptionsReachableAndSendsTheClickedVote()
    {
        IVoteHandle vote = null!;
        const string choice = "Длинное название следующего режима для узкого окна";
        await Server.WaitPost(() => vote = Server.Resolve<IVoteManager>().CreateVote(new VoteOptions
        {
            Title = "Следующий режим",
            InitiatorText = "Длинное имя инициатора голосования",
            Options = Enumerable.Range(0, 12).Select(i => ($"{choice} {i}", (object) i)).ToList(),
        }));
        await Pair.RunTicksSync(10);
        await Client.WaitPost(() =>
        {
            var ui = Client.Resolve<IUserInterfaceManager>();
            foreach (var window in ui.WindowRoot.Children.OfType<BaseWindow>().ToArray())
                window.Close();
        });
        await Pair.RunTicksSync(3);
        Button first = null!;
        await Client.WaitAssertion(() =>
        {
            var lobby = ((LobbyState) Client.Resolve<Robust.Client.State.IStateManager>().CurrentState).Lobby!;
            var popup = lobby.VoteContainer.Children.OfType<VotePopup>().Single();
            var options = popup.FindControl<BoxContainer>("VoteOptionsContainer").Children.OfType<Button>().ToArray();
            var scroll = popup.FindControl<ScrollContainer>("VoteOptionsScroll");
            Assert.That(popup.VisibleInTree, Is.True);
            Assert.That(options, Has.Length.EqualTo(12));
            Assert.That(scroll.Height, Is.InRange(1, 196));
            Assert.That(options[^1].GlobalPosition.Y, Is.GreaterThan(scroll.GlobalPosition.Y + scroll.Height));
            Assert.That(options[0].Width, Is.LessThanOrEqualTo(scroll.Width + 1));
            first = options[0];
            var pointer = new ScreenCoordinates(first.GlobalPixelPosition + first.PixelSize / 2, first.Window!.Id);
            var hit = Client.Resolve<IUserInterfaceManager>().MouseGetControl(pointer);
            Assert.That(OrbitraKeyboardNavigation.Contains(first, hit), Is.True,
                $"Курсор попал в {hit?.GetType().Name ?? "пустоту"} вместо кнопки голосования.");

        });
        await Click(first);
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() => Assert.That(vote.CastVotes[ServerSession!], Is.Zero));
    }

    private void Key(Keyboard.Key key)
    {
        var input = Client.Resolve<IInputManager>();
        input.KeyDown(new KeyEventArgs(key, false, false, false, false, false, 0));
        input.KeyUp(new KeyEventArgs(key, false, false, false, false, false, 0));
    }

    private async Task Click(Control control)
    {
        var position = new ScreenCoordinates(control.GlobalPixelPosition + control.PixelSize / 2, control.Window?.Id ?? default);
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
        {
            var args = new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, state, position, default,
                control.Size / 2, control.PixelSize / 2);
            await Client.DoGuiEvent(control, args);
        }
    }

    [Test]
    public async Task GhostRoleNotificationKeepsButtonAndBarSizeAcrossHover()
    {
        GhostGui gui = null!;
        Button button = null!;
        Vector2 buttonSize = default, barSize = default;
        var setState = typeof(Control).GetMethod("SetOnlyStylePseudoClass", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await Client.WaitPost(() =>
        {
            gui = new GhostGui();
            Client.Resolve<IUserInterfaceManager>().StateRoot.AddChild(gui);
            gui.Update(1, true);
            button = All(gui).OfType<Button>().Single(b => b.Name == "GhostRolesButton");
        });
        try
        {
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                Assert.That(button.HasStyleClass("OrbitraGhostRolesAvailable"), Is.True);
                buttonSize = button.DesiredSize;
                barSize = gui.DesiredSize;
                Assert.That(buttonSize.X, Is.GreaterThan(0));
            });
            foreach (var state in new[] { "hover", "pressed", "normal", "hover", "normal" })
            {
                await Client.WaitPost(() => setState.Invoke(button, new object[] { state }));
                await Pair.RunTicksSync(2);
                await Client.WaitAssertion(() =>
                {
                    Assert.That(button.DesiredSize, Is.EqualTo(buttonSize), state);
                    Assert.That(gui.DesiredSize, Is.EqualTo(barSize), state);
                });
            }
            await Client.WaitPost(() => button.RemoveStyleClass("OrbitraGhostRolesAvailable"));
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                Assert.That(button.DesiredSize, Is.EqualTo(buttonSize));
                Assert.That(gui.DesiredSize, Is.EqualTo(barSize));
            });
        }
        finally
        {
            await Client.WaitPost(() => gui.Dispose());
        }
    }

    [Test]
    public async Task CyrillicNameSurvivesRepeatedClientAndServerSaves()
    {
        var preferences = Client.Resolve<IClientPreferencesManager>();
        await Client.WaitPost(() => Client.Resolve<IConfigurationManager>().SetCVar(CCVars.RestrictedNames, true));
        await Server.WaitPost(() => Server.Resolve<IConfigurationManager>().SetCVar(CCVars.RestrictedNames, true));
        foreach (var name in new[] { "Дуглас Лесли", "Ёлка Соловьёва", "Алио-Тейл", "John Smith", "Дуглас Лесли" })
        {
            await Client.WaitPost(() => preferences.UpdateCharacter(preferences.Preferences.SelectedCharacter.WithName(name),
                preferences.Preferences.SelectedCharacterIndex));
            await Pair.RunTicksSync(10);
            await Client.WaitAssertion(() => Assert.That(preferences.Preferences.SelectedCharacter.Name, Is.EqualTo(name)));
            await Server.WaitAssertion(() => Assert.That(Server.Resolve<IServerPreferencesManager>()
                .GetPreferences(ServerSession!.UserId).SelectedCharacter.Name, Is.EqualTo(name)));
        }
    }

    [Test]
    public async Task ManifestWrapsColumnsAndGhostRulesKeepTextWithoutTimer()
    {
        RoundEndSummaryWindow summary = null!;
        GhostRolesWindow roles = null!;
        GhostRoleRulesWindow rules = null!;
        await Server.WaitPost(() => Server.Resolve<IConfigurationManager>().SetCVar(CCVars.GhostRoleTime, 0f));
        await Pair.RunTicksSync(5);
        await Client.WaitPost(() =>
        {
            summary = new RoundEndSummaryWindow("Secret", "Round result", TimeSpan.FromMinutes(20), 1,
            [new RoundEndMessageEvent.RoundEndPlayerInfo
            {
                PlayerOOCName = "LongAccountName",
                PlayerICName = "Длинное имя персонажа для проверки переноса",
                Role = "job-name-captain",
                Antag = true,
            }]);
            All(summary).OfType<TabContainer>().Single().CurrentTab = 1;
            roles = new GhostRolesWindow();
            roles.OpenCentered();
            roles.AddEntry("Мышь", "Длинное описание роли с переносом текста внутри карточки.", true, null,
                [new GhostRoleInfo { Identifier = 1, Kind = GhostRoleKind.FirstComeFirstServe },
                 new GhostRoleInfo { Identifier = 2, Kind = GhostRoleKind.FirstComeFirstServe }], CEntMan.System<SpriteSystem>());
            rules = new GhostRoleRulesWindow("Rules remain visible", _ => {});
            rules.OpenCentered();
        });
        try
        {
            foreach (var width in new[] { 900f, 600f, 420f })
            {
                await Client.WaitPost(() => summary.SetSize = new Vector2(width, 500));
                await Pair.RunTicksSync(3);
                await Client.WaitAssertion(() =>
                {
                    Assert.That(summary.HasStyleClass("OrbitraEntryWindow"), Is.True);
                    Assert.That(roles.HasStyleClass("OrbitraEntryWindow"), Is.True);
                    Assert.That(rules.HasStyleClass("OrbitraEntryWindow"), Is.True);
                    Assert.That(All(roles).OfType<CollapsibleHeading>().Single().HasStyleClass("OrbitraLobbyButton"), Is.True);
                    Assert.That(All(roles).OfType<PanelContainer>().Count(p => p.HasStyleClass("OrbitraRoleCard")), Is.EqualTo(1));
                    var rows = All(summary).OfType<OrbitraManifestRow>().ToArray();
                    Assert.That(rows.Length, Is.EqualTo(2));
                    Assert.That(rows[0].ChildCount, Is.EqualTo(4));
                    Assert.That(rows[1].ChildCount, Is.EqualTo(4));
                    for (var i = 0; i < 4; i++)
                    {
                        Assert.That(rows[1].GetChild(i).Width, Is.EqualTo(rows[0].GetChild(i).Width).Within(1),
                            $"Column {i}; header {rows[0].Size}, body {rows[1].Size}; header parent {rows[0].Parent!.Size}, body parent {rows[1].Parent!.Size}; header child {rows[0].GetChild(i).DesiredSize}, body child {rows[1].GetChild(i).DesiredSize}");
                        Assert.That(rows[1].GetChild(i).Position.X + rows[1].GetChild(i).Width, Is.LessThanOrEqualTo(rows[1].Width + 1));
                    }
                    Assert.That(All(rules).OfType<RichTextLabel>().Single().Height, Is.GreaterThan(0));
                    Assert.That(All(rules).OfType<Button>().Single(b => b.Name == "RequestButton").Disabled, Is.False);
                });
            }
        }
        finally
        {
            await Client.WaitPost(() => { summary.Dispose(); roles.Dispose(); rules.Dispose(); });
        }
    }
}
