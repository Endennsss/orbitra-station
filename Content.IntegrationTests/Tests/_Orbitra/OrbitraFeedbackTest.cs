using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._Orbitra.UserInterface;
using Content.Client.Options.UI;
using Content.Client.UserInterface.Systems.Ghost.Controls;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Content.Shared.Ghost.Systems;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Input;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraFeedbackTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, InLobby = true, Fresh = true, Dirty = true, NoLoadTestPrototypes = true };

    private static IEnumerable<Control> All(Control root)
    {
        yield return root;
        foreach (var child in root.Children)
            foreach (var descendant in All(child)) yield return descendant;
    }

    private void Key(Keyboard.Key key)
    {
        var input = Client.Resolve<IInputManager>();
        input.KeyDown(new KeyEventArgs(key, false, false, false, false, false, 0));
        input.KeyUp(new KeyEventArgs(key, false, false, false, false, false, 0));
    }

    [Test]
    public async Task ActualGhostToolbarAndWindowHeadersStayCompactAcrossViewportMatrix()
    {
        var geometry = new List<(float Header, Vector2 Close, Vector2 Icon, Vector2 Action, bool SameRow, bool ListBelow)>();
        var calls = new int[3];
        var fallback = false;
        await Client.WaitPost(() =>
        {
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            using var ghost = new GhostTargetWindow();
            using var options = new OptionsMenu();
            ghost.OpenCentered();
            options.OpenCentered();
            ghost.UpdateWarps(new[]
            {
                new GhostWarp(new NetEntity(12345601), "Ada", false) { CharacterName = "Ada with a sufficiently long name", Job = "ChiefEngineer" },
                new GhostWarp(new NetEntity(12345602), "Bridge", true),
            });
            var search = ghost.FindControl<LineEdit>("SearchBar");
            var buttons = new[] { "GhostnadoButton", "WarpToRandomFollowedButton", "WarpToRandomButton" }.Select(ghost.FindControl<OrbitraButton>).ToArray();
            foreach (var resolution in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080), new Vector2(3440, 1440), new Vector2(5120, 1440) })
            foreach (var scale in new[] { 1f, 1.25f, 1.5f })
            {
                var available = resolution / scale - new Vector2(32);
                foreach (var window in new Content.Client.UserInterface.Controls.FancyWindow[] { ghost, options })
                {
                    window.MaxSize = available;
                    window.SetSize = Vector2.Min(window == ghost ? new Vector2(600, 440) : new Vector2(960, 720), available);
                    window.Measure(available);
                    window.Arrange(UIBox2.FromDimensions(Vector2.Zero, window.DesiredSize));
                }
                var close = All(ghost).OfType<OrbitraWindowCloseButton>().Single();
                var header = ghost.FindControl<PanelContainer>("WindowHeader");
                geometry.Add((header.Height, close.Size, All(close).OfType<OrbitraIcon>().Single().Size, buttons[0].Size,
                    System.Math.Abs(buttons[0].GlobalPosition.Y + buttons[0].Height / 2 - search.GlobalPosition.Y - search.Height / 2) < 1,
                    ghost.FindControl<ScrollContainer>("GhostScroll").GlobalPosition.Y >= search.GlobalPosition.Y + search.Height));
            }
            options.Close();
            ghost.OnGhostnadoClicked += () => calls[0]++;
            ghost.OnWarpToRandomFollowedClicked += () => calls[1]++;
            ghost.OnWarpToRandomClicked += () => calls[2]++;
            foreach (var button in buttons) { button.GrabKeyboardFocus(); Key(Keyboard.Key.Return); }
            ghost.MinSize = Vector2.Zero;
            ghost.SetSize = new Vector2(300, 280);
            ghost.Measure(ghost.SetSize);
            ghost.Arrange(UIBox2.FromDimensions(Vector2.Zero, ghost.SetSize));
            fallback = buttons[0].GlobalPosition.Y >= search.GlobalPosition.Y + search.Height && buttons[0].GlobalPosition.Y == buttons[2].GlobalPosition.Y;
        });
        foreach (var row in geometry)
        {
            Assert.That(row.Header, Is.EqualTo(36).Within(0.1));
            Assert.That(row.Close, Is.EqualTo(new Vector2(32)));
            Assert.That(row.Icon, Is.EqualTo(new Vector2(16)));
            Assert.That(row.Action, Is.EqualTo(new Vector2(32)));
            Assert.That(row.SameRow, Is.True, $"Toolbar: {row}");
            Assert.That(row.ListBelow, Is.True, $"List: {row}");
        }
        Assert.That(calls, Is.EqualTo(new[] { 1, 1, 1 }));
        Assert.That(fallback, Is.True);
    }

    [Test]
    public async Task ActualSettingsActionsPublishOnlyTheirOwnResult()
    {
        var kinds = new List<OrbitraNotificationKind>();
        await Client.WaitPost(() =>
        {
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            using var options = new OptionsMenu();
            options.OpenCentered();
            options.Measure(new Vector2(960, 720));
            options.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(960, 720)));
            var slot = options.FindControl<OrbitraNotification>("OrbitraFeedback");
            var controls = All(options).OfType<OptionsTabControlRow>().First(c => c.VisibleInTree);
            var check = All(options).OfType<OrbitraCheckBox>().First(c => c.VisibleInTree && !c.Disabled);
            check.GrabKeyboardFocus();
            Key(Keyboard.Key.Space);
            controls.FindControl<OrbitraButton>("ResetButton").GrabKeyboardFocus();
            Key(Keyboard.Key.Return);
            if (slot.HasMessage) kinds.Add(slot.Kind);
            check.GrabKeyboardFocus();
            Key(Keyboard.Key.Space);
            controls.FindControl<OrbitraButton>("ApplyButton").GrabKeyboardFocus();
            Key(Keyboard.Key.Return);
            if (slot.HasMessage) kinds.Add(slot.Kind);
            controls.FindControl<OrbitraButton>("DefaultButton").GrabKeyboardFocus();
            Key(Keyboard.Key.Return);
            if (slot.HasMessage) kinds.Add(slot.Kind);
        });
        Assert.That(kinds, Is.EqualTo(new[] { OrbitraNotificationKind.Info, OrbitraNotificationKind.Success, OrbitraNotificationKind.Warning }));
    }

    [Test]
    public async Task NotificationReplacementHoverPauseAndOwnerCloseKeepReservedGeometry()
    {
        var checks = new List<bool>();
        await Client.WaitPost(() =>
        {
            var ui = Client.Resolve<IUserInterfaceManager>();
            var config = Client.Resolve<IConfigurationManager>();
            config.SetCVar(CCVars.ReducedMotion, true);
            using var options = new OptionsMenu();
            options.OpenCentered();
            options.Measure(new Vector2(960, 720));
            options.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(960, 720)));
            var slot = options.FindControl<OrbitraNotification>("OrbitraFeedback");
            var before = slot.Size;
            slot.Show(OrbitraNotificationKind.Success, "Applied");
            var panel = All(slot).OfType<PanelContainer>().Single();
            ui.SetHovered(panel);
            // Headless InputManager возвращает нулевую позицию мыши; обновляем только время виджета.
            typeof(OrbitraNotification).GetMethod("FrameUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(slot, [new FrameEventArgs(5)]);
            checks.Add(slot.HasMessage);
            ui.SetHovered(null);
            slot.Show(OrbitraNotificationKind.Info, "Replacement");
            OrbitraWindowLifecycleTest.Frame(ui, 3);
            checks.Add(slot.HasMessage);
            slot.Show(OrbitraNotificationKind.Info, "Replacement");
            OrbitraWindowLifecycleTest.Frame(ui, 3);
            checks.Add(slot.HasMessage);
            OrbitraWindowLifecycleTest.Frame(ui, 1.1f);
            checks.Add(!slot.HasMessage);
            slot.Show(OrbitraNotificationKind.Warning, "Apply defaults");
            OrbitraWindowLifecycleTest.Frame(ui, 10);
            checks.Add(slot.HasMessage);
            options.Measure(new Vector2(960, 720));
            options.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(960, 720)));
            checks.Add(slot.Size == before && slot.Height == 64);
            config.SetCVar(CCVars.ReducedMotion, false);
            slot.Dismiss();
            slot.Show(OrbitraNotificationKind.Error, "Actual failure");
            config.SetCVar(CCVars.ReducedMotion, true);
            checks.Add(slot.HasMessage && slot.Kind == OrbitraNotificationKind.Error);
            OrbitraEntryWindow.RequestClose(options);
            checks.Add(!slot.HasMessage);
        });
        Assert.That(checks, Is.All.True);
    }

    [Test]
    public async Task FeedbackFadeCompletesAndReducedMotionFinishesTooltipClose()
    {
        var checks = new List<bool>();
        await Client.WaitPost(() =>
        {
            var ui = Client.Resolve<IUserInterfaceManager>();
            var config = Client.Resolve<IConfigurationManager>();
            config.SetCVar(CCVars.ReducedMotion, false);
            using var options = new OptionsMenu();
            options.OpenCentered();
            for (var i = 0; i < 16; i++) OrbitraWindowLifecycleTest.Frame(ui, 0.025f);
            var slot = options.FindControl<OrbitraNotification>("OrbitraFeedback");
            var panel = All(slot).OfType<PanelContainer>().Single();
            slot.Show(OrbitraNotificationKind.Success, "Saved");
            OrbitraWindowLifecycleTest.Frame(ui, 0);
            OrbitraWindowLifecycleTest.Frame(ui, 0.025f);
            // Появление со сдвигом анимирует контейнер, а исчезновение - саму панель.
            var motion = All(slot).OfType<OrbitraMotionHost>().Single();
            checks.Add(motion.Modulate.A > 0 && motion.Modulate.A < 1);
            for (var i = 0; i < 8; i++) OrbitraWindowLifecycleTest.Frame(ui, 0.025f);
            slot.Dismiss();
            OrbitraWindowLifecycleTest.Frame(ui, 0.025f);
            checks.Add(panel.Modulate.A > 0 && panel.Modulate.A < 1);
            for (var i = 0; i < 8; i++) OrbitraWindowLifecycleTest.Frame(ui, 0.025f);
            checks.Add(!slot.HasMessage);
            var close = All(options).OfType<OrbitraWindowCloseButton>().First();
            OrbitraKeyboardNavigation.Focus(close);
            OrbitraWindowLifecycleTest.Frame(ui, 0.3f);
            OrbitraWindowLifecycleTest.Frame(ui, 0);
            OrbitraWindowLifecycleTest.Frame(ui, 0.025f);
            var tip = ui.PopupRoot.Children.OfType<OrbitraTooltip>().SingleOrDefault();
            checks.Add(tip != null && tip.Modulate.A > 0 && tip.Modulate.A < 1);
            ui.ReleaseKeyboardFocus();
            OrbitraWindowLifecycleTest.Frame(ui, 0.025f);
            config.SetCVar(CCVars.ReducedMotion, true);
            OrbitraWindowLifecycleTest.Frame(ui, 0);
            checks.Add(!ui.PopupRoot.Children.OfType<OrbitraTooltip>().Any());
        });
        Assert.That(checks, Is.All.True);
    }

    [Test]
    public async Task TooltipPreservesSpecializedContentAndReadsReboundHotkeyAtShowTime()
    {
        var checks = new List<bool>();
        await Client.WaitPost(() =>
        {
            var ui = Client.Resolve<IUserInterfaceManager>();
            var input = Client.Resolve<IInputManager>();
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            using var window = new Content.Client.UserInterface.Controls.FancyWindow();
            using var specialized = new Label { Text = "Specialized content" };
            var button = new OrbitraButton { Text = "Action", ToolTip = "Action" };
            var custom = new OrbitraButton { Text = "Custom", TooltipSupplier = _ => specialized };
            OrbitraTooltips.Attach(button, EngineKeyFunctions.UIClick);
            window.ContentsContainer.AddChild(new BoxContainer { Children = { button, custom } });
            OrbitraEntryWindow.Attach(window);
            window.OpenCentered();
            OrbitraWindowLifecycleTest.Frame(ui, 0);
            foreach (var binding in input.GetKeyBindings(EngineKeyFunctions.UIClick).ToArray()) input.RemoveBinding(binding);
            foreach (var key in new[] { Keyboard.Key.F7, Keyboard.Key.F8 })
            {
                foreach (var binding in input.GetKeyBindings(EngineKeyFunctions.UIClick).ToArray()) input.RemoveBinding(binding);
                input.RegisterBinding(new KeyBindingRegistration
                {
                    Function = EngineKeyFunctions.UIClick,
                    BaseKey = key,
                    Mod1 = key == Keyboard.Key.F8 ? Keyboard.Key.Control : Keyboard.Key.Unknown,
                });
                ui.SetHovered(button);
                ui.GetType().GetMethod("_showTooltip", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ui, null);
                var tip = ui.PopupRoot.Children.OfType<OrbitraTooltip>().Single();
                // Headless backend не именует физические клавиши; модификатор меняет штатную строку сочетания.
                var expected = Client.Resolve<Robust.Shared.Localization.ILocalizationManager>().GetString("orbitra-ui-tooltip-hotkey",
                    ("text", "Action"), ("key", input.GetKeyFunctionButtonString(EngineKeyFunctions.UIClick)));
                var actual = All(tip).OfType<RichTextLabel>().Single().GetMessage();
                checks.Add(actual == expected && actual.Contains('+') == (key == Keyboard.Key.F8));
                ui.SetHovered(null);
                OrbitraWindowLifecycleTest.Frame(ui, 0);
            }
            ui.SetHovered(custom);
            ui.GetType().GetMethod("_showTooltip", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ui, null);
            checks.Add(ui.PopupRoot.Children.OfType<OrbitraTooltip>().Single().SuppliedContent == specialized);
            window.Close();
            checks.Add(!specialized.Disposed && specialized.Parent == null && !ui.PopupRoot.Children.OfType<OrbitraTooltip>().Any());
        });
        Assert.That(checks, Is.All.True);
    }

    [Test]
    public async Task PlaceTooltipHasNoEmptyJobLineAndPlainTooltipsKeepOnlyMeaningfulLines()
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.Resolve<IUserInterfaceManager>();
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            using var ghost = new GhostTargetWindow();
            ghost.OpenCentered();
            ghost.UpdateWarps(new[] { new GhostWarp(new NetEntity(12345603), "РНД", true) });
            OrbitraWindowLifecycleTest.Frame(ui, 0);
            var place = All(ghost).OfType<ContainerButton>().Single(button => button.ToolTip == "РНД");
            ui.SetHovered(place);
            ui.GetType().GetMethod("_showTooltip", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ui, null);
            var tip = ui.PopupRoot.Children.OfType<OrbitraTooltip>().Single();
            tip.Measure(new Vector2(360, 400));
            Assert.That(tip.DesiredSize.Y, Is.LessThan(60), "A short place name must not reserve a second text line.");
            var height = tip.DesiredSize.Y;
            foreach (var text in new[] { "РНД", "РНД\n", "РНД\r\n\r\n" })
            {
                using var plain = new OrbitraTooltip(text);
                ui.PopupRoot.AddChild(plain);
                plain.Measure(new Vector2(360, 400));
                Assert.That(plain.DesiredSize.Y, Is.EqualTo(height).Within(0.1));
            }
            using var multiline = new OrbitraTooltip("Имя\nПрофессия");
            ui.PopupRoot.AddChild(multiline);
            multiline.Measure(new Vector2(360, 400));
            Assert.That(multiline.DesiredSize.Y, Is.GreaterThan(height));
            using var literal = new OrbitraTooltip("[bold]РНД[/bold]");
            Assert.That(literal.GetChild(0), Is.TypeOf<RichTextLabel>());
            Assert.That(((RichTextLabel) literal.GetChild(0)).GetFormattedMessage()!.ToString(), Is.EqualTo("[bold]РНД[/bold]"));
            OrbitraEntryWindow.RequestClose(ghost);
        });
    }

    [Test]
    public async Task NativeAndKeyboardTooltipsStaySingleAndCleanUpWithOwner()
    {
        var checks = new List<bool>();
        await Client.WaitPost(() =>
        {
            var ui = Client.Resolve<IUserInterfaceManager>();
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            using var ghost = new GhostTargetWindow();
            ghost.OpenCentered();
            OrbitraWindowLifecycleTest.Frame(ui, 0);
            var button = ghost.FindControl<OrbitraButton>("GhostnadoButton");
            ui.SetHovered(button);
            // Входим в штатный путь показа после задержки без аппаратного курсора headless-клиента.
            ui.GetType().GetMethod("_showTooltip", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ui, null);
            checks.Add(ui.PopupRoot.Children.OfType<OrbitraTooltip>().Count() == 1);
            var tip = ui.PopupRoot.Children.OfType<OrbitraTooltip>().Single();
            tip.Measure(new Vector2(360, 400));
            tip.Arrange(UIBox2.FromDimensions(tip.Position, tip.DesiredSize));
            checks.Add(tip.Width > 0 && tip.Width <= 360 && tip.GlobalPosition.X >= 16 && tip.GlobalPosition.Y >= 16);
            ui.SetHovered(null);
            OrbitraWindowLifecycleTest.Frame(ui, 0.1f);
            var next = ghost.FindControl<OrbitraButton>("WarpToRandomButton");
            OrbitraKeyboardNavigation.Focus(next);
            OrbitraWindowLifecycleTest.Frame(ui, 0.3f);
            checks.Add(ui.PopupRoot.Children.OfType<OrbitraTooltip>().Count() == 1 && next.HasKeyboardFocus());
            OrbitraEntryWindow.RequestClose(ghost);
            checks.Add(!ui.PopupRoot.Children.OfType<OrbitraTooltip>().Any());
        });
        Assert.That(checks, Is.All.True);
    }
}
