using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Content.Client.Administration.UI;
using Content.Client.Administration.UI.Bwoink;
using Content.Client.Construction.UI;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Actions.Windows;
using Content.Client.UserInterface.Systems.Character.Windows;
using Content.Client.UserInterface.Systems.Sandbox.Windows;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Input;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraGameMenusTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true, NoLoadTestPrototypes = true };
    private static readonly Vector2[] Resolutions = [new(1280, 720), new(1920, 1080), new(2560, 1080), new(3440, 1440), new(5120, 1440)];

    private static IEnumerable<Control> All(Control root)
    {
        yield return root;
        foreach (var child in root.Children)
        foreach (var nested in All(child))
            yield return nested;
    }

    [Test]
    public async Task SixRealWindowsKeepCompactChromeAndContentAcrossViewportMatrix()
    {
        var geometry = new List<(string Window, float Header, Vector2 Close, bool Fits, float Title)>();
        var constructionLayouts = new List<bool>();
        await Client.WaitPost(() =>
        {
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            FancyWindow[] windows = [new SandboxWindow(), new AdminMenuWindow(), new CharacterWindow(),
                new ConstructionMenu(), new BwoinkWindow(), new ActionsWindow()];
            try
            {
                foreach (var window in windows)
                {
                    window.OpenCentered();
                    foreach (var resolution in Resolutions)
                    foreach (var scale in new[] { 1f, 1.25f, 1.5f })
                    {
                        var available = resolution / scale - new Vector2(32);
                        window.MaxSize = available;
                        window.MinSize = Vector2.Min(window.MinSize, available);
                        window.SetSize = Vector2.Min(new Vector2(900, 680), available);
                        window.Measure(available);
                        window.Arrange(UIBox2.FromDimensions(Vector2.Zero, window.DesiredSize));
                        var close = All(window).OfType<OrbitraWindowCloseButton>().Single();
                        geometry.Add((window.GetType().Name, window.FindControl<PanelContainer>("WindowHeader").Height,
                            close.Size, window.Width <= available.X + 1 && window.Height <= available.Y + 1,
                            window.FindControl<Label>("WindowTitle").Width));
                        if (window is ConstructionMenu)
                        {
                            var layout = All(window).OfType<OrbitraAdaptiveRow>().First();
                            constructionLayouts.Add((layout.Orientation == BoxContainer.LayoutOrientation.Vertical) == (layout.Width < 720));
                        }
                    }
                    window.Close();
                    window.OpenCentered();
                    window.Close();
                }
            }
            finally
            {
                foreach (var window in windows) window.Dispose();
            }
        });
        Assert.That(geometry, Has.Count.EqualTo(90));
        foreach (var row in geometry)
        {
            Assert.That(row.Header, Is.EqualTo(36).Within(0.1), row.Window);
            Assert.That(row.Close, Is.EqualTo(new Vector2(32)), row.Window);
            Assert.That(row.Fits, Is.True, row.Window);
            Assert.That(row.Title, Is.GreaterThan(150), row.Window);
        }
        Assert.That(constructionLayouts, Is.All.True);
    }

    [Test]
    public async Task LobbyDockEdgesAndCenterSurviveChatWidthAndVisibilityChanges()
    {
        var rows = new List<(float Width, float Top, float Bottom, float Center)>();
        await Client.WaitPost(() =>
        {
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            var lobby = ((LobbyState) Client.Resolve<IStateManager>().CurrentState).Lobby!;
            foreach (var resolution in Resolutions)
            foreach (var scale in new[] { 1f, 1.25f, 1.5f })
            foreach (var chatWidth in new[] { 360f, 400f, 480f })
            {
                var size = resolution / scale;
                lobby.SetOrbitraChatWidth(chatWidth);
                lobby.Measure(size);
                lobby.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                lobby.Measure(size);
                lobby.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                var info = lobby.FindControl<PanelContainer>("InfoPanel");
                var chat = lobby.FindControl<PanelContainer>("RightSide");
                var dock = lobby.FindControl<BoxContainer>("InfoDock");
                if (!dock.Visible) continue;
                rows.Add((info.Width, (info.GlobalPosition.Y - chat.GlobalPosition.Y) * scale,
                    (info.GlobalPosition.Y + info.Height - chat.GlobalPosition.Y - chat.Height) * scale,
                    (lobby.CharacterPreview.GlobalPosition.X + lobby.CharacterPreview.Width / 2 - size.X / 2) * scale));
                var height = info.Height;
                chat.Visible = false;
                lobby.Measure(size);
                lobby.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                rows.Add((info.Width, 0, info.Height - height,
                    (lobby.CharacterPreview.GlobalPosition.X + lobby.CharacterPreview.Width / 2 - size.X / 2) * scale));
                chat.Visible = true;
            }
        });
        Assert.That(rows, Is.Not.Empty);
        foreach (var row in rows)
        {
            Assert.That(row.Width, Is.EqualTo(300).Within(0.1));
            Assert.That(row.Top, Is.EqualTo(0).Within(1));
            Assert.That(row.Bottom, Is.EqualTo(0).Within(1));
            Assert.That(row.Center, Is.EqualTo(0).Within(1));
        }
    }

    [Test]
    public async Task NativeSpawnersAreExplicitlyStyledAndUnrelatedWindowsStayNative()
    {
        var flags = new List<bool>();
        await Client.WaitPost(() =>
        {
            var ui = Client.Resolve<IUserInterfaceManager>();
            OrbitraEngineMenus.Ensure(ui);
            using var entities = new EntitySpawnWindow();
            using var tiles = new TileSpawnWindow();
            using var unrelated = new DefaultWindow();
            foreach (var window in new BaseWindow[] { entities, tiles, unrelated })
                window.OpenCentered();
            flags.Add(entities.HasStyleClass("OrbitraEntryWindow"));
            flags.Add(tiles.HasStyleClass("OrbitraEntryWindow"));
            flags.Add(!unrelated.HasStyleClass("OrbitraEntryWindow"));
            flags.Add(All(entities).OfType<OrbitraWindowCloseButton>().Count() == 1);
            entities.Close();
            entities.Open();
            flags.Add(All(entities).OfType<OrbitraWindowCloseButton>().Count() == 1);
        });
        Assert.That(flags, Is.All.True);
    }

    [Test]
    public async Task DirectToolsKeepOneChromeAcrossRepeatedOpenings()
    {
        var flags = new List<bool>();
        await Client.WaitPost(() =>
        {
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            FancyWindow[] windows = [
                new Content.Client.Decals.UI.DecalPlacerWindow(),
                new Content.Client.Administration.UI.BanList.BanListWindow(),
                new Content.Client.Administration.UI.BanPanel.BanPanel(),
                new AdminAnnounceWindow(),
                new Content.Client.Administration.UI.Tabs.AdminTab.AdminShuttleWindow(),
                new Content.Client.Administration.UI.EventLog.AdminEventLogWindow(),
                new Content.Client.Administration.UI.Logs.AdminLogsWindow(),
                new Content.Client.Fax.AdminUI.AdminFaxWindow(),
                new Content.Client.Administration.UI.Tabs.AdminbusTab.LoadBlueprintsWindow(),
                new Content.Client.Administration.UI.Tabs.AtmosTab.AddAtmosWindow(),
                new Content.Client.Administration.UI.Tabs.AtmosTab.AddGasWindow(),
                new Content.Client.Administration.UI.Tabs.AtmosTab.FillGasWindow(),
                new Content.Client.Administration.UI.Tabs.AtmosTab.SetTemperatureWindow(),
                new Content.Client.Administration.UI.Tabs.PanicBunkerTab.PanicBunkerStatusWindow()
            ];
            try
            {
                foreach (var window in windows)
                for (var repeat = 0; repeat < 3; repeat++)
                {
                    window.OpenCentered();
                    window.Measure(new Vector2(800, 440));
                    window.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(800, 440)));
                    flags.Add(window.HasStyleClass("OrbitraEntryWindow") &&
                        All(window).OfType<OrbitraWindowCloseButton>().Count() == 1);
                    window.Close();
                }
            }
            finally
            {
                foreach (var window in windows) window.Dispose();
            }
        });
        Assert.That(flags, Has.Count.EqualTo(42));
        Assert.That(flags, Is.All.True);
    }

    [Test]
    public async Task RealFilterPopupRetainsSelectionAndOwnership()
    {
        ActionsWindow window = null!;
        Popup popup = null!;
        await Client.WaitPost(() =>
        {
            window = new ActionsWindow();
            window.OpenCentered();
            window.Measure(new Vector2(500, 400));
            window.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(500, 400)));
        });
        await Click(window.FilterButton);
        await Client.WaitAssertion(() =>
        {
            popup = Client.Resolve<IUserInterfaceManager>().ModalRoot.Children.OfType<Popup>().Single(p => p.Visible);
            Assert.That(OrbitraMotion.CanAnimate(popup), Is.True);
            Assert.That(All(popup).OfType<Button>().All(b => b.HasStyleClass("OrbitraEditorControl")), Is.True);
        });
        var calls = 0;
        await Client.WaitPost(() => window.FilterButton.OnItemSelected += _ => calls++);
        await Click(All(popup).OfType<Button>().First());
        await Client.WaitAssertion(() =>
        {
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(window.FilterButton.SelectedKeys, Has.Count.EqualTo(1));
            Assert.That(popup.Visible, Is.False);
        });
        await Client.WaitPost(() => window.Dispose());
    }

    [Test]
    public async Task AdminToolButtonOpensFancyWindowAndReusesIt()
    {
        AdminMenuWindow menu = null!;
        Content.Client.Administration.UI.CustomControls.UICommandButton button = null!;
        await Client.WaitPost(() =>
        {
            menu = new AdminMenuWindow();
            menu.OpenCentered();
            button = All(menu).OfType<Content.Client.Administration.UI.CustomControls.UICommandButton>()
                .Single(b => b.WindowType == typeof(Content.Client.Administration.UI.Tabs.AdminTab.AdminShuttleWindow));
        });
        await Click(button);
        BaseWindow tool = null!;
        await Client.WaitAssertion(() =>
        {
            tool = Client.Resolve<IUserInterfaceManager>().WindowRoot.Children
                .OfType<Content.Client.Administration.UI.Tabs.AdminTab.AdminShuttleWindow>().Single();
            Assert.That(tool.HasStyleClass("OrbitraEntryWindow"), Is.True);
        });
        await Client.WaitPost(() => tool.Close());
        await Click(button);
        await Client.WaitAssertion(() =>
        {
            Assert.That(tool.IsOpen, Is.True);
            Assert.That(Client.Resolve<IUserInterfaceManager>().WindowRoot.Children
                .OfType<Content.Client.Administration.UI.Tabs.AdminTab.AdminShuttleWindow>().Single(), Is.SameAs(tool));
        });
        await Client.WaitPost(() => { tool.Dispose(); menu.Dispose(); });
    }

    [Test]
    public async Task SandboxCloseButtonReversesAndReducedMotionCompletesClosure()
    {
        SandboxWindow window = null!;
        var closed = 0;
        await Client.WaitPost(() =>
        {
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, false);
            window = new SandboxWindow();
            window.OnClose += () => closed++;
            window.OpenCentered();
            window.Measure(new Vector2(500, 600));
            window.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(320, 600)));
        });
        for (var i = 0; i < 3; i++)
        {
            await Click(All(window).OfType<OrbitraWindowCloseButton>().Single());
            await Client.WaitAssertion(() => Assert.That(OrbitraEntryWindow.IsClosing(window), Is.True));
            await Client.WaitPost(() => window.Open());
            await Client.WaitAssertion(() =>
            {
                Assert.That(OrbitraEntryWindow.IsClosing(window), Is.False);
                Assert.That(window.IsOpen, Is.True);
                Assert.That(closed, Is.Zero);
            });
        }
        await Click(All(window).OfType<OrbitraWindowCloseButton>().Single());
        await Client.WaitPost(() => Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true));
        await Pair.RunTicksSync(2);
        await Client.WaitAssertion(() =>
        {
            Assert.That(window.IsOpen, Is.False);
            Assert.That(closed, Is.EqualTo(1));
        });
        await Client.WaitPost(() => window.Dispose());
    }

    private async Task Click(Control control)
    {
        var position = new ScreenCoordinates(control.GlobalPixelPosition + control.PixelSize / 2, control.Window?.Id ?? default);
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
        {
            var args = new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, state, position, false,
                control.Size / 2, control.PixelSize / 2);
            await Client.DoGuiEvent(control, args);
        }
    }

    [Test]
    public async Task AdminTabsReleasePreviousSelectionWithoutWaitingForLayout()
    {
        AdminMenuWindow window = null!;
        Button[] buttons = [];
        await Client.WaitPost(() =>
        {
            window = new AdminMenuWindow();
            window.OpenCentered();
            buttons = All(window).OfType<Button>().Where(b => b.HasStyleClass("OrbitraJournalTab") && b.ToggleMode).ToArray();
        });
        try
        {
            await Pair.RunTicksSync(2);
            foreach (var index in new[] { 2, 3, 4, 2, 2, 0 })
            {
                await Click(buttons[index]);
                await Client.WaitAssertion(() =>
                {
                    Assert.That(buttons.Count(b => b.Pressed), Is.EqualTo(1));
                    Assert.That(buttons[index].Pressed, Is.True);
                    Assert.That(window.FindControl<TabContainer>("MasterTabContainer").CurrentTab, Is.EqualTo(index));
                });
                // Стили применяются очередью UI в следующем кадре, состояние выбора — сразу.
                await Pair.RunTicksSync(2);
                await Client.WaitAssertion(() =>
                {
                    foreach (var button in buttons)
                    {
                        Assert.That(button.TryGetStyleProperty<Robust.Client.Graphics.StyleBox>(ContainerButton.StylePropertyStyleBox, out var style), Is.True);
                        Assert.That(style, Is.TypeOf<Robust.Client.Graphics.StyleBoxFlat>());
                        Assert.That(((Robust.Client.Graphics.StyleBoxFlat) style!).BorderThickness.Bottom,
                            Is.EqualTo(button.Pressed ? 2 : 0));
                    }
                });
            }
            await Client.WaitPost(() => window.FindControl<TabContainer>("MasterTabContainer").CurrentTab = 1);
            await Client.WaitAssertion(() =>
            {
                Assert.That(buttons.Count(b => b.Pressed), Is.EqualTo(1));
                Assert.That(buttons[1].Pressed, Is.True);
            });
        }
        finally
        {
            await Client.WaitPost(() => window.Dispose());
        }
    }

    [Test]
    public async Task HelpFooterSeparatesPreferencesAndRetainsControlsAcrossResize()
    {
        var fits = new List<bool>();
        await Client.WaitPost(() =>
        {
            using var window = new BwoinkWindow();
            window.OpenCentered();
            var preferences = All(window).OfType<WrapContainer>().Single(c => c.Name == "OrbitraHelpPreferences");
            var actions = All(window).OfType<WrapContainer>().Single(c => c.Name == "OrbitraHelpActions");
            var original = actions.Children.ToArray();
            foreach (var action in original) action.Visible = true;
            foreach (var width in new[] { 640, 900, 1100 })
            {
                window.SetSize = new Vector2(width, 500);
                window.Measure(new Vector2(width, 500));
                window.Arrange(UIBox2.FromDimensions(Vector2.Zero, window.DesiredSize));
                fits.Add(preferences.GlobalPosition.Y + preferences.Height <= actions.GlobalPosition.Y);
                fits.Add(original.SequenceEqual(actions.Children));
                fits.Add(original.All(c => c.GlobalPosition.X + c.Width <= window.GlobalPosition.X + window.Width));
                fits.Add(preferences.Children.OfType<CheckBox>().Count() == 2);
                fits.Add(!actions.Children.OfType<CheckBox>().Any());
            }
        });
        Assert.That(fits, Is.All.True);
    }

    [Test]
    public async Task SandboxScrollbarKeepsHitAreaAndContentGutter()
    {
        SandboxWindow window = null!;
        await Client.WaitPost(() =>
        {
            window = new SandboxWindow();
            window.SetSize = new Vector2(320, 420);
            window.OpenCentered();
        });
        try
        {
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() =>
            {
                var bar = All(window).OfType<VScrollBar>().Single();
                Assert.That(bar.VisibleInTree, Is.True);
                Assert.That(bar.Width, Is.GreaterThanOrEqualTo(12));
                Assert.That(window.SpawnTilesButton.GlobalPosition.X + window.SpawnTilesButton.Width,
                    Is.LessThanOrEqualTo(bar.GlobalPosition.X - 7));
            });
        }
        finally
        {
            await Client.WaitPost(() => window.Dispose());
        }
    }

    [Test]
    public async Task PopulatedConstructionRowsAndFooterRetainTheirStyledGeometry()
    {
        var rowOverrides = new List<bool>();
        var footerFits = new List<bool>();
        await Client.WaitPost(() =>
        {
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            var prototypes = Client.Resolve<Robust.Shared.Prototypes.IPrototypeManager>();
            using var window = new ConstructionMenu();
            window.OpenCentered();
            var entity = prototypes.Index<Robust.Shared.Prototypes.EntityPrototype>("ChairWood");
            var recipes = prototypes.EnumeratePrototypes<Content.Shared.Construction.Prototypes.ConstructionPrototype>()
                .Take(30).Select(p => new ConstructionMenu.ConstructionMenuListData(p, entity)).ToArray();
            window.ListViewRecipes.PopulateList(recipes);
            window.SetRecipeInfo("Очень длинное название выбранного рецепта", "Описание рецепта с длинным текстом для проверки переноса.", entity, false, false);
            foreach (var width in new[] { 420f, 640f, 820f, 1100f })
            {
                window.SetSize = new Vector2(width, 560);
                window.Measure(new Vector2(width, 560));
                window.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(width, 560)));
                foreach (var row in All(window.ListViewRecipes).OfType<ListContainerButton>())
                    rowOverrides.Add(row.StyleBoxOverride == null && row.HasStyleClass("OrbitraOptionRow"));
                foreach (var name in new[] { "ClearButton", "EraseButton" })
                {
                    var button = window.FindControl<Button>(name);
                    footerFits.Add(!button.ClipText && button.Label.Width + button.Label.Margin.Left + button.Label.Margin.Right + 1 >= button.Label.DesiredSize.X &&
                        button.Position.X + button.Width <= button.Parent!.Width + 1 &&
                        button.GlobalPosition.X + button.Width <= window.GlobalPosition.X + window.Width + 1);
                }
            }
            using var actions = new ActionsWindow();
            actions.OpenCentered();
            rowOverrides.Add(!actions.SearchBar.HasStyleClass("actionSearchBox"));
        });
        Assert.That(rowOverrides, Has.Count.GreaterThan(4));
        Assert.That(rowOverrides, Is.All.True);
        Assert.That(footerFits, Is.All.True);
    }

    [Test]
    public async Task PopulatedAdminObjectTableAlignsHeaderAndRows()
    {
        var aligned = new List<bool>();
        var diagnostics = new List<string>();
        AdminMenuWindow window = null!;
        await Client.WaitPost(() =>
        {
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            window = new AdminMenuWindow();
            window.OpenCentered();
            window.FindControl<TabContainer>("MasterTabContainer").CurrentTab = 7;
        });
        var page = window.ObjectsTabControl;
        var list = page.SearchList;
        try
        {
            foreach (var count in new[] { 3, 30 })
            {
                await Client.WaitPost(() => list.PopulateList(Enumerable.Range(1, count).Select(i =>
                    new Content.Client.Administration.UI.Tabs.ObjectsTab.ObjectsListData(
                        ($"Grid {i}", new Robust.Shared.GameObjects.NetEntity(i)), $"Grid {i}",
                        Content.Client._Orbitra.Stylesheets.OrbitraPalettes.PanelBackground)).ToArray()));
                foreach (var width in new[] { 820f, 1100f, 1230f })
                {
                    await Client.WaitPost(() => window.SetSize = new Vector2(width, 560));
                    // Два кадра: раскладка списка и отложенное согласование заголовка.
                    await Pair.RunTicksSync(2);
                    await Client.WaitPost(() =>
                    {
                        var header = page.FindControl<Control>("ListHeader");
                        var headerLabel = header.FindControl<Label>("EntityIDLabel");
                        var bar = list.Children.OfType<VScrollBar>().Single();
                        foreach (var row in All(list).OfType<ListContainerButton>())
                        {
                            var cell = All(row).OfType<Label>().Single(label => label.Name == "EIDLabel");
                            diagnostics.Add($"{count}/{width}: delta={cell.GlobalPosition.X - headerLabel.GlobalPosition.X}, margin={header.Margin.Right}, bar={bar.Visible}/{bar.DesiredSize.X}, row={row.Width}, header={header.Width}");
                            aligned.Add(row.StyleBoxOverride == null &&
                                Math.Abs(cell.GlobalPosition.X - headerLabel.GlobalPosition.X) <= 2);
                        }
                    });
                }
            }
        }
        finally
        {
            await Client.WaitPost(() => window.Dispose());
        }
        Assert.That(aligned, Has.Count.GreaterThan(9));
        Assert.That(aligned, Is.All.True, string.Join("\n", diagnostics));
    }
}
