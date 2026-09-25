using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Content.Client.Guidebook.Controls;
using Content.Client.Options.UI;
using Content.Client.UserInterface.Systems.Ghost.Controls;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Content.Shared.Ghost.Systems;
using Content.Shared.Guidebook;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Client.UserInterface.Systems.Info;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraKeyboardTest : GameTest
{
    private static readonly ProtoId<GuideEntryPrototype> NewPlayerPrototype = "NewPlayer";

    public override PoolSettings PoolSettings => new() { Connected = true, InLobby = true, Fresh = true, Dirty = true, NoLoadTestPrototypes = true };

    private void Key(Keyboard.Key key, bool shift = false, bool control = false)
    {
        var input = Client.Resolve<IInputManager>();
        input.KeyDown(new KeyEventArgs(key, false, false, control, shift, false, 0));
        input.KeyUp(new KeyEventArgs(key, false, false, control, shift, false, 0));
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        yield return root;
        foreach (var child in root.Children)
            foreach (var nested in Descendants(child))
                yield return nested;
    }

    [Test]
    public async Task OptionsKeyboardTogglesOnceAndTraversesBothDirections()
    {
        var toggled = false;
        var nextFocused = false;
        var returned = false;
        await Client.WaitPost(() =>
        {
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            using var options = new OptionsMenu();
            options.OpenCentered();
            options.SetSize = new Vector2(900, 700);
            options.Measure(options.SetSize);
            options.Arrange(UIBox2.FromDimensions(Vector2.Zero, options.SetSize));
            var check = Descendants(options).OfType<OrbitraCheckBox>().First(c => c.VisibleInTree && !c.Disabled);
            var before = check.Pressed;
            check.GrabKeyboardFocus();
            Key(Keyboard.Key.Space);
            toggled = check.Pressed == !before;
            Key(Keyboard.Key.Tab);
            nextFocused = Client.Resolve<IUserInterfaceManager>().KeyboardFocused != check;
            Key(Keyboard.Key.Tab, shift: true);
            returned = Client.Resolve<IUserInterfaceManager>().KeyboardFocused == check;
        });
        Assert.Multiple(() =>
        {
            Assert.That(toggled, Is.True);
            Assert.That(nextFocused, Is.True);
            Assert.That(returned, Is.True);
        });
    }

    [Test]
    public async Task OptionsDropdownKeyboardUsesOriginalSelectionHandler()
    {
        var calls = 0;
        var selected = -1;
        var expected = -2;
        var returned = false;
        await Client.WaitPost(() =>
        {
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            using var options = new OptionsMenu();
            options.OpenCentered();
            options.SetSize = new Vector2(900, 700);
            options.Measure(options.SetSize);
            options.Arrange(UIBox2.FromDimensions(Vector2.Zero, options.SetSize));
            var dropdown = Descendants(options).OfType<OrbitraOptionButton>().First(c => c.VisibleInTree && c.ItemCount > 1);
            OrbitraOptionButton.BindSelection(dropdown, args => { selected = args.Id; calls++; });
            dropdown.GrabKeyboardFocus();
            Key(Keyboard.Key.Return);
            Key(Keyboard.Key.Home);
            Key(Keyboard.Key.Return);
            expected = dropdown.GetItemId(0);
            returned = dropdown.HasKeyboardFocus();
        });
        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(selected, Is.EqualTo(expected));
            Assert.That(returned, Is.True);
        });
    }

    [Test]
    public async Task GuideSearchKeyboardOpensResultAndClearsQuery()
    {
        var searchFocused = false;
        var resultFocused = false;
        var selected = false;
        var cleared = false;
        var open = false;
        await Client.WaitPost(() =>
        {
            using var guide = new GuidebookWindow();
            var entry = CProtoMan.Index(NewPlayerPrototype);
            guide.UpdateGuides(new Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry> { ["NewPlayer"] = entry }, selected: "NewPlayer");
            guide.OpenCentered();
            guide.SetSize = new Vector2(900, 700);
            guide.Measure(guide.SetSize);
            guide.Arrange(UIBox2.FromDimensions(Vector2.Zero, guide.SetSize));
            var search = guide.FindControl<LineEdit>("OrbitraArticleSearch");
            Key(Keyboard.Key.F, control: true);
            searchFocused = search.HasKeyboardFocus();
            search.SetText(Client.Resolve<Robust.Shared.Localization.ILocalizationManager>().GetString(entry.Name), true);
            Key(Keyboard.Key.Down);
            resultFocused = Client.Resolve<IUserInterfaceManager>().KeyboardFocused is OrbitraContainerButton;
            Key(Keyboard.Key.Return);
            selected = guide.Selected?.Id == "NewPlayer";
            Key(Keyboard.Key.F, control: true);
            Key(Keyboard.Key.Escape);
            cleared = search.Text.Length == 0;
            open = guide.IsOpen;
        });
        Assert.Multiple(() =>
        {
            Assert.That(searchFocused, Is.True);
            Assert.That(resultFocused, Is.True);
            Assert.That(selected, Is.True);
            Assert.That(cleared, Is.True);
            Assert.That(open, Is.True);
        });
    }

    [Test]
    public async Task GhostKeyboardSelectsOnceAndRejectsClosedResponse()
    {
        var count = 0;
        NetEntity selected = default;
        var accepted = true;
        var target = new NetEntity(12345901);
        var focusPreserved = false;
        var neighborFocused = false;
        await Client.WaitPost(() =>
        {
            using var window = new GhostTargetWindow();
            window.UpdateWarps(new[] { new GhostWarp(target, "Ada", false) { CharacterName = "Ada", Job = "ChiefEngineer" } });
            window.OpenCentered();
            window.WarpClicked += id => { selected = id; count++; };
            Key(Keyboard.Key.F, control: true);
            Key(Keyboard.Key.Down);
            Key(Keyboard.Key.Return);
            var ui = Client.Resolve<IUserInterfaceManager>();
            var row = ui.KeyboardFocused;
            window.UpdateWarps(new[] { new GhostWarp(target, "Ada", false) { CharacterName = "Ada", Job = "Captain" } });
            focusPreserved = ui.KeyboardFocused == row;
            window.UpdateWarps(new[] { new GhostWarp(new NetEntity(12345902), "Bob", false) { CharacterName = "Bob", Job = "Captain" } });
            neighborFocused = ui.KeyboardFocused is OrbitraContainerButton neighbor && neighbor.ToolTip?.StartsWith("Bob\n") == true;
            window.Close();
            accepted = window.AcceptOrbitraResponse();
        });
        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(1));
            Assert.That(selected, Is.EqualTo(target));
            Assert.That(accepted, Is.False);
            Assert.That(focusPreserved, Is.True);
            Assert.That(neighborFocused, Is.True);
        });
    }

    [Test]
    public async Task ClosingReturnsFocusAndReversingRestoresLastControl()
    {
        var returned = false;
        var reversed = false;
        var closed = false;
        var closeCount = 0;
        await Client.WaitPost(() =>
        {
            var ui = Client.Resolve<IUserInterfaceManager>();
            var config = Client.Resolve<IConfigurationManager>();
            config.SetCVar(CCVars.ReducedMotion, true);
            using var parent = new OptionsMenu();
            parent.OpenCentered();
            var origin = Descendants(parent).OfType<OrbitraCheckBox>().First(c => c.VisibleInTree);
            origin.GrabKeyboardFocus();
            using var child = new GuidebookWindow();
            child.OpenCentered();
            var input = child.FindControl<ScrollContainer>("Scroll");
            input.GrabKeyboardFocus();
            config.SetCVar(CCVars.ReducedMotion, false);
            child.OnClose += () => closeCount++;
            ui.GetUIController<CloseRecentWindowUIController>().CloseMostRecentWindow();
            returned = origin.HasKeyboardFocus();
            child.OpenCentered();
            reversed = input.HasKeyboardFocus();
            ui.GetUIController<CloseRecentWindowUIController>().CloseMostRecentWindow();
            config.SetCVar(CCVars.ReducedMotion, true);
            OrbitraWindowLifecycleTest.Frame(ui, 0);
            closed = !child.IsOpen;
        });
        Assert.Multiple(() =>
        {
            Assert.That(returned, Is.True);
            Assert.That(reversed, Is.True);
            Assert.That(closed, Is.True);
            Assert.That(closeCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GhostTimeoutRetryAndLateResponseUseSinglePendingRequest()
    {
        var initial = false;
        var duplicate = true;
        var retry = false;
        var received = false;
        var error = false;
        await Client.WaitPost(() =>
        {
            using var window = new GhostTargetWindow();
            window.OpenCentered();
            initial = window.BeginOrbitraRequest();
            duplicate = window.BeginOrbitraRequest();
            // Таймаут проверяется через реальный FrameUpdate открытого окна.
            OrbitraWindowLifecycleTest.Frame(Client.Resolve<IUserInterfaceManager>(), 10.1f);
            error = Descendants(window).OfType<OrbitraStatusPanel>().Single().ActionButton.Visible;
            retry = window.BeginOrbitraRequest();
            received = window.AcceptOrbitraResponse();
        });
        Assert.Multiple(() =>
        {
            Assert.That(initial, Is.True);
            Assert.That(duplicate, Is.False);
            Assert.That(error, Is.True);
            Assert.That(retry, Is.True);
            Assert.That(received, Is.True);
        });
    }

    [Test]
    public async Task RebindIgnoresOpeningReleaseAndReceivesTab()
    {
        var prompt = false;
        var registered = 0;
        var stayed = false;
        await Client.WaitPost(() =>
        {
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            using var window = new OptionsMenu();
            window.OpenCentered();
            window.FindControl<TabContainer>("Tabs").CurrentTab = 2;
            window.SetSize = new Vector2(900, 700);
            window.Measure(window.SetSize);
            window.Arrange(UIBox2.FromDimensions(Vector2.Zero, window.SetSize));
            var button = Descendants(window).OfType<OrbitraButton>().First(c => c.Parent?.GetType().Name == "BindButton" && !c.Disabled);
            button.GrabKeyboardFocus();
            Key(Keyboard.Key.Return);
            prompt = button.Text == Client.Resolve<Robust.Shared.Localization.ILocalizationManager>().GetString("ui-options-key-prompt");
            var input = Client.Resolve<IInputManager>();
            void Registered(IKeyBinding _) => registered++;
            input.OnKeyBindingAdded += Registered;
            try
            {
                OrbitraWindowLifecycleTest.Frame(Client.Resolve<IUserInterfaceManager>(), 0);
                Key(Keyboard.Key.Tab);
                stayed = button.HasKeyboardFocus();
            }
            finally
            {
                input.OnKeyBindingAdded -= Registered;
            }
        });
        Assert.Multiple(() =>
        {
            Assert.That(prompt, Is.True);
            Assert.That(registered, Is.EqualTo(1));
            Assert.That(stayed, Is.True);
        });
    }

    [Test]
    public async Task OptionsSliderUsesArrowStepAndBounds()
    {
        var minimum = false;
        var maximum = false;
        var stepped = false;
        await Client.WaitPost(() =>
        {
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            using var window = new OptionsMenu();
            window.OpenCentered();
            window.FindControl<TabContainer>("Tabs").CurrentTab = 3;
            var slider = Descendants(window).OfType<Slider>().First(c => c.VisibleInTree && !c.Disabled);
            slider.GrabKeyboardFocus();
            Key(Keyboard.Key.End);
            maximum = slider.Value == slider.MaxValue;
            Key(Keyboard.Key.Home);
            minimum = slider.Value == slider.MinValue;
            Key(Keyboard.Key.Right);
            stepped = slider.Value > slider.MinValue && slider.Value <= slider.MaxValue;
        });
        Assert.Multiple(() =>
        {
            Assert.That(minimum, Is.True);
            Assert.That(maximum, Is.True);
            Assert.That(stepped, Is.True);
        });
    }
}
