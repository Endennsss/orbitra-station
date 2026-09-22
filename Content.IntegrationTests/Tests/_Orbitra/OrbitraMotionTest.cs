using System.Numerics;
using Content.Client._Orbitra.Lobby;
using Content.Client.Options.UI;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraMotionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true };

    [Test]
    public async Task DrawerReversalPreservesGeometryAndRestoresInput()
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var ui = Pair.Client.Resolve<IUserInterfaceManager>();
            Pair.Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, false);
            using var host = new OrbitraMotionHost();
            var button = new Button { Text = "Test", CanKeyboardFocus = true };
            host.AddChild(button);
            ui.StateRoot.AddChild(host);
            Layout(host);
            var size = host.DesiredSize;
            host.SetShown(true, new Vector2(-16, 0));
            Layout(host);
            var runner = Runner(ui);
            runner.Presented();
            runner.Advance(0, false);
            runner.Advance(0.08f, false);
            host.SetShown(false, new Vector2(-16, 0));
            Layout(host);
            Assert.That(button.MouseFilter, Is.EqualTo(Control.MouseFilterMode.Ignore));
            Assert.That(button.CanKeyboardFocus, Is.False);
            runner.Presented();
            runner.Advance(0, false);
            runner.Advance(0.04f, false);
            Layout(host);
            var offset = button.Position;
            var alpha = host.Modulate.A;
            host.SetShown(true, new Vector2(-16, 0));
            Layout(host);
            Assert.That(button.Position, Is.EqualTo(offset));
            Assert.That(host.Modulate.A, Is.EqualTo(alpha).Within(0.001));
            Assert.That(button.CanKeyboardFocus, Is.True);
            Layout(host);
            runner.Presented();
            runner.Advance(0, false);
            Complete(runner);
            Layout(host);
            Assert.That(host.Modulate, Is.EqualTo(Color.White));
            Assert.That(host.DesiredSize, Is.EqualTo(size));
            Assert.That(button.Position, Is.EqualTo(Vector2.Zero));
            host.SetShown(false, new Vector2(-16, 0));
            Layout(host);
            runner.Presented();
            runner.Advance(0, false);
            Complete(runner);
            Assert.That(host.Visible, Is.False);
            Assert.That(button.VisibleInTree, Is.False);
            Assert.That(button.CanKeyboardFocus, Is.True);
        });
    }

    [Test]
    public async Task ReducedMotionResizeAndDisposalFinishTransitions()
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var ui = Pair.Client.Resolve<IUserInterfaceManager>();
            var config = Pair.Client.Resolve<IConfigurationManager>();
            config.SetCVar(CCVars.ReducedMotion, false);
            using var host = new OrbitraMotionHost();
            host.AddChild(new Button { Text = "Test" });
            ui.StateRoot.AddChild(host);
            Layout(host);
            host.Reveal(0.2f, new Vector2(0, 12));
            Layout(host);
            host.InvalidateMeasure();
            var runner = Runner(ui);
            runner.Presented();
            runner.Advance(0, false);
            runner.Advance(0.05f, false);
            Assert.That(host.Modulate.A, Is.InRange(0.01f, 0.99f));
            runner.Advance(0, true);
            Assert.That(host.Modulate, Is.EqualTo(Color.White));
            host.Reveal(0.2f, new Vector2(0, 12));
            Layout(host);
            runner.Presented();
            runner.Advance(0, false);
            runner.Advance(0.01f, false);
            host.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(400, 120)));
            runner.Advance(0.01f, false);
            Assert.That(host.Modulate.A, Is.InRange(0.01f, 0.99f));
            config.SetCVar(CCVars.ReducedMotion, true);
            host.Reveal(0.2f, new Vector2(0, 12));
            Assert.That(host.Modulate, Is.EqualTo(Color.White));
            config.SetCVar(CCVars.ReducedMotion, false);
            host.Reveal(0.2f, new Vector2(0, 12));
            host.Dispose();
            Assert.DoesNotThrow(() => runner.Advance(1, false));
            Assert.That(runner.ActiveCount, Is.Zero);
        });
    }

    [Test]
    public async Task RepeatedTabRevealRestoresOpacityAndKeepsSelectedControl()
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var ui = Pair.Client.Resolve<IUserInterfaceManager>();
            Pair.Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, false);
            using var tabs = new TabContainer();
            var first = new LineEdit { Text = "Unchanged" };
            var second = new LineEdit { Text = "Second" };
            tabs.AddChild(first);
            tabs.AddChild(second);
            ui.StateRoot.AddChild(tabs);
            OrbitraMotion.AttachTabs(tabs);
            tabs.CurrentTab = 1;
            Layout(tabs);
            var runner = Runner(ui);
            runner.Presented();
            runner.Advance(0, false);
            runner.Advance(0.05f, false);
            tabs.CurrentTab = 0;
            tabs.CurrentTab = 1;
            Layout(tabs);
            runner.Presented();
            runner.Advance(0, false);
            Complete(runner);
            Assert.That(second.Modulate, Is.EqualTo(Color.White));
            Assert.That(tabs.CurrentTab, Is.EqualTo(1));
            Assert.That(first.Text, Is.EqualTo("Unchanged"));
            Assert.That(first.VisibleInTree, Is.False);
            Assert.That(tabs.ChildCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ReopenedWindowWaitsForLayoutAndReplaysFade()
    {
        OptionsMenu window = null!;
        OrbitraMotion runner = null!;
        await Pair.Client.WaitPost(() =>
        {
            Pair.Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, false);
            window = new OptionsMenu();
        });
        for (var i = 0; i < 3; i++)
        {
            await Pair.Client.WaitPost(() =>
            {
                window.OpenCentered();
                window.SetSize = new Vector2(700, 500);
                window.Measure(window.SetSize);
                window.Arrange(UIBox2.FromDimensions(Vector2.Zero, window.SetSize));
                OrbitraMotion.Reveal(window, OrbitraMotion.WindowDuration);
                runner = Runner(Pair.Client.Resolve<IUserInterfaceManager>());
                runner.Presented();
            runner.Advance(0, false);
                window.SetSize = new Vector2(720, 520);
                window.Measure(window.SetSize);
                window.Arrange(UIBox2.FromDimensions(Vector2.Zero, window.SetSize));
                runner.Advance(0.01f, false);
                runner.Advance(0.04f, false);
            });
            await Pair.Client.WaitAssertion(() => Assert.That(window.Modulate.A, Is.InRange(0.01f, 0.99f)));
            await Pair.Client.WaitPost(() => Complete(runner));
            await Pair.Client.WaitAssertion(() => Assert.That(window.Modulate, Is.EqualTo(Color.White)));
            await Pair.Client.WaitPost(window.Close);
        }
        await Pair.Client.WaitPost(window.Dispose);
    }

    [Test]
    public async Task PopupCloseCancelsFadeBeforeImmediateReopen()
    {
        Popup popup = null!;
        OptionsMenu owner = null!;
        await Pair.Client.WaitPost(() =>
        {
            Pair.Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, false);
            owner = new OptionsMenu();
            owner.OpenCentered();
            popup = new Popup();
            Pair.Client.Resolve<IUserInterfaceManager>().ModalRoot.AddChild(popup);
            OrbitraMotion.BindPopup(popup, owner);
            popup.Open();
            Layout(popup);
            OrbitraMotion.Reveal(popup, OrbitraMotion.MenuDuration);
            var runner = Runner(Pair.Client.Resolve<IUserInterfaceManager>());
            runner.Presented();
            runner.Advance(0, false);
            runner.Advance(0.04f, false);
            popup.Close();
        });
        await Pair.Client.WaitAssertion(() => Assert.That(popup.Modulate, Is.EqualTo(Color.White)));
        await Pair.Client.WaitPost(() =>
        {
            popup.Open();
            OrbitraMotion.Reveal(popup, OrbitraMotion.MenuDuration);
        });
        await Pair.Client.WaitAssertion(() => Assert.That(popup.Modulate.A, Is.Zero));
        await Pair.Client.WaitPost(() =>
        {
            popup.Close();
            popup.Dispose();
            owner.Dispose();
        });
    }

    private static void Complete(OrbitraMotion runner)
    {
        runner.Presented();
        for (var i = 0; i < 12; i++)
            runner.Advance(0.05f, false);
    }

    private static void Layout(Control control)
    {
        control.Measure(new Vector2(320, 120));
        control.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(320, 120)));
    }

    private static OrbitraMotion Runner(IUserInterfaceManager ui)
    {
        foreach (var child in ui.RootControl.Children)
        {
            if (child is OrbitraMotion runner)
                return runner;
        }
        throw new InvalidOperationException("Missing motion runner");
    }
}
