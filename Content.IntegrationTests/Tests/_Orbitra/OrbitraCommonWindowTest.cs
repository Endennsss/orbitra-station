using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Content.Client._Orbitra.Lobby;
using Content.Client.Options.UI;
using Content.Client.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Content.Shared.Guidebook;

using Content.Client._Orbitra.UserInterface; // Orbitra-Edit

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraCommonWindowTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, InLobby = false, Dirty = true, Fresh = true };

    [Test]
    public async Task ActualMenuAndOptionsKeepContentAtAllViewportSizes()
    {
        var results = new List<(float MenuWidth, float TitleWidth, float LabelWidth, bool Tooltip, bool Fits)>();
        var actionsFit = new List<bool>();
        await Pair.Client.WaitPost(() =>
        {
            using var menu = new EscapeMenu();
            using var options = new OptionsMenu();
            menu.OpenCentered();
            options.OpenCentered();
            foreach (var screen in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080), new Vector2(3440, 1440), new Vector2(5120, 1440) })
            foreach (var scale in new[] { 1f, 1.25f, 1.5f })
            {
                var available = screen / scale - new Vector2(32);
                menu.MaxSize = available;
                menu.Measure(available);
                menu.Arrange(UIBox2.FromDimensions(Vector2.Zero, menu.DesiredSize));
                var scroll = Descendants(menu).OfType<ScrollContainer>().First();
                var actionList = scroll.Children.OfType<BoxContainer>().Single();
                if (actionList.DesiredSize.Y <= scroll.Height)
                    actionsFit.Add(menu.QuitButton.GlobalPosition.Y + menu.QuitButton.Height <= scroll.GlobalPosition.Y + scroll.Height + 1);
                else
                {
                    scroll.SetScrollValue(new Vector2(0, 10000));
                    scroll.Arrange(UIBox2.FromDimensions(scroll.Position, scroll.Size));
                    actionsFit.Add(menu.QuitButton.GlobalPosition.Y + menu.QuitButton.Height <= scroll.GlobalPosition.Y + scroll.Height + 1);
                    scroll.SetScrollValue(Vector2.Zero);
                }
                options.SetSize = Vector2.Min(new Vector2(960, 720), available);
                options.MaxSize = available;
                options.Measure(available);
                options.Arrange(UIBox2.FromDimensions(Vector2.Zero, options.DesiredSize));
                foreach (var check in Descendants(options).OfType<CheckBox>().Where(c => c.VisibleInTree && !string.IsNullOrEmpty(c.Text)))
                    results.Add((menu.Width, menu.FindControl<Label>("WindowTitle").Width, check.Label.Width,
                        check.ToolTip == check.Text, menu.Height <= available.Y + 1 && check.Label.Width <= check.Width));
            }
            menu.Close();
            menu.OpenCentered();
            options.Close();
            options.OpenCentered();
        });
        Assert.That(results, Is.Not.Empty);
        Assert.That(actionsFit, Is.All.True);
        foreach (var result in results)
        {
            Assert.That(result.MenuWidth, Is.EqualTo(320).Within(1));
            Assert.That(result.TitleWidth, Is.GreaterThan(200));
            Assert.That(result.LabelWidth, Is.GreaterThan(100));
            Assert.That(result.Tooltip, Is.True);
            Assert.That(result.Fits, Is.True);
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }

    [Test]
    public async Task CommonWindowStylesDynamicChildrenAndOwnsPopupsInRound()
    {
        FancyWindow window = null!;
        DefaultWindow unrelated = null!;
        Button dynamicButton = null!;
        Popup popup = null!;
        await Pair.Client.WaitPost(() =>
        {
            window = new FancyWindow();
            OrbitraEntryWindow.Attach(window);
            OrbitraEntryWindow.Attach(window);
            window.OpenCentered();
            dynamicButton = new Button { Text = "Dynamic" };
            window.ContentsContainer.AddChild(dynamicButton);
            unrelated = new DefaultWindow();
            unrelated.OpenCentered();
            popup = new Popup();
            Pair.Client.Resolve<IUserInterfaceManager>().ModalRoot.AddChild(popup);
            OrbitraMotion.BindPopup(popup, dynamicButton);
            popup.Open();
        });
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(window.HasStyleClass("OrbitraWindowChrome"), Is.True);
            Assert.That(dynamicButton.HasStyleClass("OrbitraSurfaceAttached"), Is.True);
            Assert.That(OrbitraMotion.CanAnimate(dynamicButton), Is.True);
            Assert.That(OrbitraMotion.CanAnimate(popup), Is.True);
            Assert.That(unrelated.HasStyleClass("OrbitraEntryWindow"), Is.False);
            Assert.That(OrbitraMotion.CanAnimate(unrelated), Is.False);
        });
        await Pair.Client.WaitPost(() => window.Close());
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(OrbitraMotion.CanAnimate(popup), Is.False);
            Assert.That(window.Modulate, Is.EqualTo(Color.White));
        });
        await Pair.Client.WaitPost(() =>
        {
            popup.Close();
            popup.Dispose();
            unrelated.Dispose();
            window.Dispose();
        });
    }

    [Test]
    public async Task GuideSectionsSwitchWithoutRebuildingTree()
    {
        Content.Client.Guidebook.Controls.GuidebookWindow guide = null!;
        Control tree = null!;
        await Pair.Client.WaitPost(() =>
        {
            guide = new Content.Client.Guidebook.Controls.GuidebookWindow();
            guide.UpdateGuides(Pair.Client.Resolve<IPrototypeManager>().EnumeratePrototypes<GuideEntryPrototype>()
                .ToDictionary(p => new ProtoId<GuideEntryPrototype>(p.ID), p => (GuideEntry) p), selected: "NewPlayer");
            guide.OpenCentered();
            tree = guide.Tree;
        });
        var sizes = new List<Vector2> { new(900, 600), new(500, 600), new(800, 600), new(480, 600), new(900, 600) };
        foreach (var screen in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080), new Vector2(3440, 1440), new Vector2(5120, 1440) })
        foreach (var scale in new[] { 1f, 1.25f, 1.5f })
            sizes.Add(Vector2.Min(new Vector2(900, 700), screen / scale - new Vector2(32)));
        var expectedTreeWidth = 260f;
        foreach (var size in sizes)
        {
            await Pair.Client.WaitPost(() =>
            {
                guide.SetSize = size;
                guide.MaxSize = guide.SetSize;
                guide.Measure(guide.SetSize);
                guide.Arrange(UIBox2.FromDimensions(Vector2.Zero, guide.SetSize));
            });
            await Pair.Client.WaitAssertion(() =>
            {
                var wide = guide.ContentsContainer.Width >= 720;
                Assert.That(guide.FindControl<Button>("OrbitraSections").Visible, Is.EqualTo(!wide));
                Assert.That(guide.FindControl<BoxContainer>("TreeBox").Visible, Is.EqualTo(wide));
                Assert.That(guide.Tree, Is.SameAs(tree));
                Assert.That(guide.Selected?.Id, Is.EqualTo("NewPlayer"));
                Assert.That(guide.Tree.Items.Count, Is.GreaterThan(2));
                if (wide)
                {
                    var split = guide.FindControl<SplitContainer>("Split");
                    Assert.That(split.First!.Width, Is.EqualTo(expectedTreeWidth).Within(1));
                    Assert.That(split.Second!.Width, Is.GreaterThanOrEqualTo(400));
                    Assert.That(split.Second.Position.X, Is.GreaterThanOrEqualTo(split.First.Width));
                }
            });
            if (expectedTreeWidth == 260)
            {
                await Pair.Client.WaitPost(() =>
                {
                    var split = guide.FindControl<SplitContainer>("Split");
                    split.SplitCenter = 325;
                    split.Measure(split.Size);
                    split.Arrange(UIBox2.FromDimensions(split.Position, split.Size));
                });
                expectedTreeWidth = 320;
            }
        }
        await Pair.Client.WaitPost(guide.Dispose);
    }

    [Test]
    public async Task AdaptiveActionsKeepChildrenAndValuesAcrossWidths()
    {
        OrbitraAdaptiveRow row = null!;
        LineEdit field = null!;
        await Pair.Client.WaitPost(() =>
        {
            row = new OrbitraAdaptiveRow();
            field = new LineEdit { Text = "Keep me", HorizontalExpand = true };
            row.AddChild(field);
            row.AddChild(new Button { Text = "Action", HorizontalExpand = true });
        });
        foreach (var width in new[] { 900, 480, 559, 560, 900 })
        {
            await Pair.Client.WaitPost(() =>
            {
                row.Measure(new Vector2(width, 300));
                row.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(width, 300)));
            });
            await Pair.Client.WaitAssertion(() =>
            {
                Assert.That(row.Orientation, Is.EqualTo(width < 560 ? BoxContainer.LayoutOrientation.Vertical : BoxContainer.LayoutOrientation.Horizontal));
                Assert.That(field.Text, Is.EqualTo("Keep me"));
                Assert.That(row.GetChild(0), Is.SameAs(field));
                Assert.That(field.Width, Is.LessThanOrEqualTo(width));
            });
        }
        await Pair.Client.WaitPost(row.Dispose);
    }
}
