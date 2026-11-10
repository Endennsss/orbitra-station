// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture, NonParallelizable]
public sealed class OrbitraDefaultThemeTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task OrdinaryWindowsAutomaticallyStyleDynamicChildrenWithoutChangingLayout(bool engineWindow)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        BaseWindow window = null!;
        Button button = null!, later = null!;
        CheckBox checkbox = null!;
        ScrollContainer scroll = null!;
        try
        {
            await pair.Client.WaitPost(() =>
            {
                window = engineWindow ? new DefaultWindow() : new FancyWindow();
                var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3 };
                button = new Button { Text = "Compact", MinWidth = 75, MinHeight = 18, SetHeight = 22, ClipText = false };
                checkbox = new CheckBox { Text = "Inline", HorizontalExpand = false, MinHeight = 19 };
                scroll = new ScrollContainer { HScrollEnabled = true };
                scroll.AddChild(new Control { SetSize = new Vector2(600, 100) });
                body.AddChild(button);
                body.AddChild(checkbox);
                body.AddChild(scroll);
                Contents(window).AddChild(body);
                window.SetSize = new Vector2(400, 300);
                window.OpenCentered();
                later = new Button { Text = "Added later" };
                body.AddChild(later);
            });
            await pair.RunTicksSync(2);
            await pair.Client.WaitAssertion(() => Assert.Multiple(() =>
            {
                Assert.That(window.HasStyleClass("OrbitraWindowChrome"), Is.True);
                Assert.That(window.HasStyleClass("OrbitraEntryWindow"), Is.True);
                Assert.That(button.HasStyleClass("OrbitraEditorControl"), Is.True);
                Assert.That(later.HasStyleClass("OrbitraEditorControl"), Is.True);
                Assert.That(scroll.HScrollEnabled, Is.True);
                Assert.That(button.MinWidth, Is.EqualTo(75));
                Assert.That(button.MinHeight, Is.EqualTo(18));
                Assert.That(button.SetHeight, Is.EqualTo(22));
                Assert.That(button.ClipText, Is.False);
                Assert.That(checkbox.MinHeight, Is.EqualTo(19));
                Assert.That(checkbox.TextureRect.Size, Is.EqualTo(new Vector2(20)));
                Assert.That(checkbox.HorizontalExpand, Is.False);
            }));
        }
        finally
        {
            await pair.Client.WaitPost(() => window?.Dispose());
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplicitNativeWindowAndSubtreeArePreserved()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        FancyWindow native = null!, normal = null!;
        Button untouched = null!;
        try
        {
            await pair.Client.WaitPost(() =>
            {
                native = new FancyWindow();
                native.AddStyleClass("OrbitraNativePresentation");
                normal = new FancyWindow();
                var subtree = new BoxContainer();
                subtree.AddStyleClass("OrbitraNativePresentation");
                untouched = new Button { Text = "Native", MinHeight = 17 };
                subtree.AddChild(untouched);
                normal.ContentsContainer.AddChild(subtree);
                native.OpenCentered();
                normal.OpenCentered();
            });
            await pair.RunTicksSync(2);
            await pair.Client.WaitAssertion(() => Assert.Multiple(() =>
            {
                Assert.That(native.HasStyleClass("OrbitraEntryWindow"), Is.False);
                Assert.That(native.HasStyleClass("OrbitraWindowChrome"), Is.False);
                Assert.That(normal.HasStyleClass("OrbitraWindowChrome"), Is.True);
                Assert.That(untouched.HasStyleClass("OrbitraEditorControl"), Is.False);
                Assert.That(untouched.MinHeight, Is.EqualTo(17));
            }));
        }
        finally
        {
            await pair.Client.WaitPost(() => { native?.Dispose(); normal?.Dispose(); });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReopenDoesNotDuplicateChromeOrNativeCloseCallbacks()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        FancyWindow window = null!;
        var closed = 0;
        try
        {
            await pair.Client.WaitPost(() =>
            {
                window = new FancyWindow();
                window.OnClose += () => closed++;
                window.OpenCentered();
                window.Close();
                window.OpenCentered();
                window.Open();
            });
            await pair.RunTicksSync(2);
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(closed, Is.EqualTo(1));
                Assert.That(Descendants(window).OfType<OrbitraWindowCloseButton>().Count(), Is.EqualTo(1));
            });
        }
        finally
        {
            await pair.Client.WaitPost(() => window?.Dispose());
        }
        await pair.CleanReturnAsync();
    }

    private sealed class StateButton : Button
    {
        public void ShowState(string state) => SetOnlyStylePseudoClass(state);
    }

    [TestCase(null)]
    [TestCase("negative")]
    [TestCase("positive")]
    [TestCase("OrbitraCompactRow")]
    [TestCase("OpenLeft")]
    [TestCase("OpenRight")]
    [TestCase("OpenBoth")]
    public async Task CommonButtonStatesKeepVisibleSurfacesAndStableMargins(string? style)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        FancyWindow window = null!;
        StateButton button = null!;
        var margins = new List<Vector2>();
        var backgrounds = new List<Color>();
        try
        {
            await pair.Client.WaitPost(() =>
            {
                window = new FancyWindow();
                button = new StateButton { Text = "Compact", SetHeight = 22 };
                if (style != null)
                    button.AddStyleClass(style);
                window.ContentsContainer.AddChild(button);
                window.OpenCentered();
            });
            foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
            {
                await pair.Client.WaitPost(() => button.ShowState(state));
                await pair.RunTicksSync(2);
                await pair.Client.WaitAssertion(() =>
                {
                    Assert.That(button.TryGetStyleProperty<StyleBoxFlat>(ContainerButton.StylePropertyStyleBox, out var box), Is.True);
                    margins.Add(new Vector2(box!.GetContentMargin(StyleBox.Margin.Top), box.GetContentMargin(StyleBox.Margin.Bottom)));
                    backgrounds.Add(box.BackgroundColor);
                    Assert.That(button.ActualModulateSelf, Is.EqualTo(Color.White), $"Unexpected tint in the {state} state.");
                });
            }
            Assert.That(margins.Distinct().Count(), Is.EqualTo(1), "Hover and press must not move or clip compact button text.");
            Assert.That(backgrounds[0].R, Is.GreaterThan(0.23f));
            Assert.That(backgrounds[1].R, Is.GreaterThan(backgrounds[0].R));
            Assert.That(backgrounds[2].R, Is.GreaterThan(backgrounds[1].R));
            Assert.That(backgrounds[3].R, Is.LessThan(backgrounds[0].R));
        }
        finally
        {
            await pair.Client.WaitPost(() => window?.Dispose());
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplicitEditorLayoutCanStillBeAppliedAfterTheDefaultAppearance()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        FancyWindow window = null!;
        Button button = null!;
        ScrollContainer scroll = null!;
        OptionButton option = null!;
        try
        {
            await pair.Client.WaitPost(() =>
            {
                window = new FancyWindow();
                option = new OptionButton();
                option.AddItem("One", 0);
                option.AddItem("Two", 1);
                window.ContentsContainer.AddChild(option);
                button = new Button { Text = "Editor", MinWidth = 90 };
                scroll = new ScrollContainer { HScrollEnabled = true };
                scroll.AddChild(button);
                window.ContentsContainer.AddChild(scroll);
                window.OpenCentered();
                OrbitraEditorStyles.Apply(window);
            });
            await pair.RunTicksSync(2);
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(button.MinWidth, Is.Zero);
                Assert.That(scroll.HScrollEnabled, Is.False);
                Assert.That(Descendants(window).OfType<OrbitraWindowCloseButton>().Count(), Is.EqualTo(1));
                var rows = Descendants(option.OptionsScroll).OfType<Button>().ToArray();
                Assert.That(rows, Has.Length.EqualTo(2));
                foreach (var row in rows)
                    Assert.That(row.Children.Count(child => child.GetType().Name == "SelectionMark"), Is.EqualTo(1));
            });
        }
        finally
        {
            await pair.Client.WaitPost(() => window?.Dispose());
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AuthoredPositionsAndMaximumSizesSurviveOpenAndReopen()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        FancyWindow window = null!;
        try
        {
            await pair.Client.WaitPost(() =>
            {
                window = new FancyWindow { SetSize = new Vector2(320, 240), MaxSize = new Vector2(380, 350) };
                window.Open(new Vector2(70, 80));
            });
            await pair.RunTicksSync(2);
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(window.Position, Is.EqualTo(new Vector2(70, 80)));
                Assert.That(window.MaxSize, Is.EqualTo(new Vector2(380, 350)));
            });
            await pair.Client.WaitPost(() =>
            {
                window.Close();
                window.SetSize = new Vector2(280, 220);
                window.MaxSize = new Vector2(300, 250);
                window.Open(new Vector2(90, 110));
            });
            await pair.RunTicksSync(2);
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(window.Position, Is.EqualTo(new Vector2(90, 110)));
                Assert.That(window.MaxSize, Is.EqualTo(new Vector2(300, 250)));
            });
        }
        finally
        {
            await pair.Client.WaitPost(() => window?.Dispose());
        }
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task WindowsFitTheViewportAndRestoreTheirPreferredSize(bool engineWindow)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var cfg = pair.Client.ResolveDependency<IConfigurationManager>();
        var scale = cfg.GetCVar(CVars.DisplayUIScale);
        var autoScale = cfg.GetCVar(CVars.ResAutoScaleEnabled);
        var preferred = new Vector2(800, 500);
        var minimum = new Vector2(640, 400);
        var maximum = new Vector2(1000, 600);
        BaseWindow window = null!;
        OrbitraAdaptiveRow row = null!;
        try
        {
            await pair.Client.WaitPost(() =>
            {
                cfg.SetCVar(CVars.ResAutoScaleEnabled, false);
                cfg.SetCVar(CVars.DisplayUIScale, 1f);
                window = engineWindow ? new DefaultWindow() : new FancyWindow();
                window.MinSize = minimum;
                window.MaxSize = maximum;
                window.SetSize = preferred;
                row = new OrbitraAdaptiveRow();
                row.AddChild(new Button { Text = "First" });
                row.AddChild(new Button { Text = "Second" });
                Contents(window).AddChild(row);
                window.OpenCentered();
            });
            await pair.RunTicksSync(3);
            await pair.Client.WaitAssertion(() =>
                Assert.That(row.Orientation, Is.EqualTo(BoxContainer.LayoutOrientation.Horizontal)));

            await pair.Client.WaitPost(() => cfg.SetCVar(CVars.DisplayUIScale, 2.5f));
            await pair.RunTicksSync(4);
            await pair.Client.WaitAssertion(() => Assert.Multiple(() =>
            {
                Assert.That(window.Position.X, Is.GreaterThanOrEqualTo(0));
                Assert.That(window.Position.Y, Is.GreaterThanOrEqualTo(0));
                Assert.That(window.Rect.Right, Is.LessThanOrEqualTo(window.Parent!.Width + 1));
                Assert.That(window.Rect.Bottom, Is.LessThanOrEqualTo(window.Parent!.Height + 1));
                Assert.That(window.MaxSize, Is.EqualTo(maximum));
                Assert.That(row.Orientation, Is.EqualTo(BoxContainer.LayoutOrientation.Vertical));
            }));

            await pair.Client.WaitPost(() => cfg.SetCVar(CVars.DisplayUIScale, 1f));
            await pair.RunTicksSync(4);
            await pair.Client.WaitAssertion(() => Assert.Multiple(() =>
            {
                Assert.That(window.Size, Is.EqualTo(preferred));
                Assert.That(window.SetSize, Is.EqualTo(preferred));
                Assert.That(window.MinSize, Is.EqualTo(minimum));
                Assert.That(row.Orientation, Is.EqualTo(BoxContainer.LayoutOrientation.Horizontal));
            }));

            await pair.Client.WaitPost(() =>
            {
                window.SetWidth = preferred.X + 100;
                window.SetHeight = maximum.Y + 100;
                cfg.SetCVar(CVars.DisplayUIScale, 2.5f);
            });
            await pair.RunTicksSync(4);
            await pair.Client.WaitAssertion(() => Assert.That(window.SetWidth, Is.LessThan(maximum.X)));
            await pair.Client.WaitPost(window.Close);
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(window.SetWidth, Is.EqualTo(preferred.X + 100));
                Assert.That(window.SetHeight, Is.EqualTo(maximum.Y + 100));
                Assert.That(window.MinSize, Is.EqualTo(minimum));
            });
        }
        finally
        {
            await pair.Client.WaitPost(() =>
            {
                window?.Dispose();
                cfg.SetCVar(CVars.ResAutoScaleEnabled, autoScale);
                cfg.SetCVar(CVars.DisplayUIScale, scale);
            });
        }
        await pair.CleanReturnAsync();
    }

    private sealed class DragWindow : DefaultWindow
    {
        public bool CanDragAt(Vector2 point) => GetDragModeFor(point) == DragMode.Move;
    }

    [Test]
    public async Task EngineHeaderRemainsDraggableAcrossItsVisibleHeight()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        DragWindow window = null!;
        try
        {
            await pair.Client.WaitPost(() =>
            {
                window = new DragWindow { SetSize = new Vector2(320, 240) };
                window.OpenCentered();
            });
            await pair.RunTicksSync(2);
            await pair.Client.WaitAssertion(() =>
            {
                var header = window.FindControl<PanelContainer>("WindowHeader");
                Assert.That(window.CanDragAt(new Vector2(window.Width / 2, header.Height - 2)), Is.True);
            });
        }
        finally
        {
            await pair.Client.WaitPost(() => window?.Dispose());
        }
        await pair.CleanReturnAsync();
    }

    private static Control Contents(BaseWindow window) => window is FancyWindow fancy ? fancy.ContentsContainer : ((DefaultWindow) window).Contents;

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }
}
