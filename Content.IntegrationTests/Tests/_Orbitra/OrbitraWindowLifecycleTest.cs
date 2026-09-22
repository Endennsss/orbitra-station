using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using Content.Client._Orbitra.Lobby;
using Content.Client.Options.UI;
using Content.Client.UserInterface.Systems.Info;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Input;
using Robust.Shared.Map;

using Content.Client._Orbitra.UserInterface; // Orbitra-Edit

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraWindowLifecycleTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true };

    [Test]
    public async Task NativeOpenEscapeAndReopenReverseWithoutDuplicateClose()
    {
        IUserInterfaceManager ui = null!;
        OptionsMenu window = null!;
        Button input = null!;
        var closed = 0;
        var fadingAlpha = 0f;
        await Pair.Client.WaitPost(() =>
        {
            ui = Pair.Client.Resolve<IUserInterfaceManager>();
            Pair.Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, false);
            window = new OptionsMenu();
            input = new Button { Text = "Focus", CanKeyboardFocus = true };
            window.ContentsContainer.AddChild(input);
            window.OnClose += () => closed++;
            window.OpenCentered();
            Frame(ui, 0);
            Frame(ui, 0.03f);
            input.GrabKeyboardFocus();
            ui.GetUIController<CloseRecentWindowUIController>().CloseMostRecentWindow();
            Frame(ui, 0.025f);
            fadingAlpha = window.Modulate.A;
        });
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(fadingAlpha, Is.InRange(0.001f, 0.999f));
            Assert.That(input.MouseFilter, Is.EqualTo(Control.MouseFilterMode.Ignore));
            Assert.That(ui.KeyboardFocused, Is.Not.SameAs(input));
            Assert.That(closed, Is.Zero);
        });
        await Pair.Client.WaitPost(() => window.OpenCentered());
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(window.Modulate.A, Is.EqualTo(fadingAlpha).Within(0.001));
            Assert.That(input.CanKeyboardFocus, Is.True);
        });
        await Pair.Client.WaitPost(() => Frames(ui));
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(window.Modulate, Is.EqualTo(Color.White));
            Assert.That(closed, Is.Zero);
        });
        for (var i = 0; i < 3; i++)
        {
            await Pair.Client.WaitPost(() =>
            {
                ui.GetUIController<CloseRecentWindowUIController>().CloseMostRecentWindow();
                Frames(ui);
            });
            var expected = i + 1;
            await Pair.Client.WaitAssertion(() =>
            {
                Assert.That(window.IsOpen, Is.False);
                Assert.That(closed, Is.EqualTo(expected));
            });
            await Pair.Client.WaitPost(() =>
            {
                window.OpenCentered();
                Frame(ui, 0);
                Frame(ui, 0.03f);
            });
            await Pair.Client.WaitAssertion(() => Assert.That(window.Modulate.A, Is.InRange(0.001f, 0.999f)));
            await Pair.Client.WaitPost(() => Frames(ui));
        }
        await Pair.Client.WaitPost(() =>
        {
            OrbitraEntryWindow.RequestClose(window);
            window.Dispose();
            Frames(ui);
        });
        await Pair.Client.WaitAssertion(() => Assert.That(closed, Is.EqualTo(3)));
    }

    [Test]
    public async Task PreparationAndLongFramesCannotConsumeReveal()
    {
        OptionsMenu window = null!;
        OrbitraMotion runner = null!;
        await Pair.Client.WaitPost(() =>
        {
            var ui = Pair.Client.Resolve<IUserInterfaceManager>();
            Pair.Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, false);
            window = new OptionsMenu();
            window.OpenCentered();
            runner = Runner(ui);
            runner.Advance(0.4f, false);
        });
        await Pair.Client.WaitAssertion(() => Assert.That(window.Modulate.A, Is.Zero));
        await Pair.Client.WaitPost(() =>
        {
            window.Measure(new Vector2(800, 600));
            window.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(800, 600)));
            runner.Presented();
            runner.Advance(1, false);
        });
        await Pair.Client.WaitAssertion(() => Assert.That(window.Modulate.A, Is.InRange(0.01f, 0.99f)));
        await Pair.Client.WaitPost(() => Pair.Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true));
        await Pair.Client.WaitAssertion(() => Assert.That(window.Modulate, Is.EqualTo(Color.White)));
        await Pair.Client.WaitPost(window.Dispose);
    }

    [Test]
    public async Task NativePopupCloseReleasesModalityAndReusesContent()
    {
        IUserInterfaceManager ui = null!;
        OptionsMenu owner = null!;
        Popup popup = null!;
        BoxContainer content = null!;
        await Pair.Client.WaitPost(() =>
        {
            ui = Pair.Client.Resolve<IUserInterfaceManager>();
            Pair.Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, false);
            owner = new OptionsMenu();
            owner.OpenCentered();
            Frames(ui);
            popup = new Popup { SetSize = new Vector2(240, 180) };
            content = new BoxContainer { Children = { new Button { Text = "Original row", CanKeyboardFocus = true } } };
            popup.AddChild(content);
            OrbitraMotion.BindPopup(popup, owner);
            ui.ModalRoot.AddChild(popup);
            popup.Open();
            Frame(ui, 0);
            popup.Close();
            Frame(ui, 0);
        });
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(popup.Visible, Is.False);
            Assert.That(content.Parent, Is.Not.SameAs(popup));
            Assert.That(content.MouseFilter, Is.EqualTo(Control.MouseFilterMode.Ignore));
        });
        await Pair.Client.WaitPost(() => popup.Open());
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(content.Parent, Is.SameAs(popup));
            Assert.That(content.ChildCount, Is.EqualTo(1));
            Assert.That(content.GetChild(0).CanKeyboardFocus, Is.True);
        });
        await Pair.Client.WaitPost(() =>
        {
            popup.Close();
            Frame(ui, 0);
            content.RemoveAllChildren();
            Frames(ui);
        });
        await Pair.Client.WaitAssertion(() => Assert.That(content.Parent, Is.SameAs(popup)));
        await Pair.Client.WaitPost(() =>
        {
            popup.Dispose();
            owner.Dispose();
            Frames(ui);
        });
    }

    [Test]
    public async Task PreparationTimeoutAndRepeatedOpenPreserveOriginalTint()
    {
        IUserInterfaceManager ui = null!;
        OptionsMenu window = null!;
        OrbitraMotion runner = null!;
        var original = new Color(0.4f, 0.6f, 0.8f, 0.7f);
        var intermediate = 0f;
        await Pair.Client.WaitPost(() =>
        {
            ui = Pair.Client.Resolve<IUserInterfaceManager>();
            Pair.Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, false);
            window = new OptionsMenu { Modulate = original };
            window.OpenCentered();
            runner = Runner(ui);
            runner.Advance(0.49f, false);
        });
        await Pair.Client.WaitAssertion(() => Assert.That(window.Modulate.A, Is.Zero));
        await Pair.Client.WaitPost(() => runner.Advance(0.02f, false));
        await Pair.Client.WaitAssertion(() => Assert.That(window.Modulate, Is.EqualTo(original)));
        await Pair.Client.WaitPost(() =>
        {
            window.Close();
            window.OpenCentered();
            Frame(ui, 0);
            Frame(ui, 0.04f);
            intermediate = window.Modulate.A;
            window.OpenCentered();
        });
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(intermediate, Is.InRange(0.001f, 0.699f));
            Assert.That(window.Modulate.A, Is.EqualTo(intermediate));
        });
        await Pair.Client.WaitPost(() => Pair.Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true));
        await Pair.Client.WaitAssertion(() => Assert.That(window.Modulate, Is.EqualTo(original)));
        await Pair.Client.WaitPost(() =>
        {
            OrbitraEntryWindow.RequestClose(window);
            Frame(ui, 0);
        });
        await Pair.Client.WaitAssertion(() => Assert.That(window.IsOpen, Is.False));
        await Pair.Client.WaitPost(window.Dispose);
    }

    private static OrbitraMotion Runner(IUserInterfaceManager ui) => ui.RootControl.Children.OfType<OrbitraMotion>().Single();

    [Test]
    public async Task OptionButtonClickSelectAndRepeatedOpenKeepNativeIds()
    {
        IUserInterfaceManager ui = null!;
        OptionsMenu owner = null!;
        OptionButton option = null!;
        Popup popup = null!;
        var selected = 0;
        await Pair.Client.WaitPost(() =>
        {
            ui = Pair.Client.Resolve<IUserInterfaceManager>();
            Pair.Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, false);
            owner = new OptionsMenu();
            option = new OptionButton { SetSize = new Vector2(240, 36) };
            option.AddItem("First", 11);
            option.AddItem("Second", 29);
            option.OnItemSelected += args => { selected++; option.SelectId(args.Id); };
            owner.ContentsContainer.AddChild(option);
            owner.OpenCentered();
            Frames(ui);
        });
        for (var i = 0; i < 3; i++)
        {
            await Click(option);
            await Pair.Client.WaitPost(() =>
            {
                popup = (Popup) option.OptionsScroll.Parent!;
                Frame(ui, 0);
                Frame(ui, 0.02f);
            });
            await Pair.Client.WaitAssertion(() =>
            {
                Assert.That(popup.Visible, Is.True);
                Assert.That(popup.Modulate.A, Is.InRange(0.001f, 0.999f));
            });
            Button row = null!;
            await Pair.Client.WaitPost(() => row = Descendants(option.OptionsScroll).OfType<Button>().Single(button => button.Text == "Second"));
            await Click(row);
            var expected = i + 1;
            await Pair.Client.WaitAssertion(() =>
            {
                Assert.That(selected, Is.EqualTo(expected));
                Assert.That(option.SelectedId, Is.EqualTo(29));
                Assert.That(popup.Visible, Is.False);
            });
            await Pair.Client.WaitPost(() => Frames(ui));
        }
        await Pair.Client.WaitPost(owner.Dispose);
    }

    private async Task Click(Control control)
    {
        var position = new ScreenCoordinates(control.GlobalPixelPosition + control.PixelSize / 2, control.Window?.Id ?? default);
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
        {
            var args = new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, state, position, default,
                position.Position / control.UIScale - control.GlobalPosition, position.Position - control.GlobalPixelPosition);
            await Pair.Client.DoGuiEvent(control, args);
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

    private static void Frames(IUserInterfaceManager ui)
    {
        for (var i = 0; i < 12; i++)
            Frame(ui, 0.025f);
    }

    internal static void Frame(IUserInterfaceManager ui, float seconds)
    {
        // В headless-клиенте исполняем настоящий UI-loop, затем отмечаем границу кадра рендера.
        ui.GetType().GetMethod("FrameUpdate")!.Invoke(ui, [new FrameEventArgs(seconds)]);
        Runner(ui).Presented();
    }
}
