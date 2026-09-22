using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Content.Client.Guidebook.Controls;
using Content.Client.Options.UI;
using Content.Client.UserInterface.Systems.Ghost.Controls;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Ghost.Systems;
using Content.Shared.Guidebook;
using Content.Server.Mind;
using Content.Server.Roles;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraUiKitTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, InLobby = true, Fresh = true, Dirty = true, NoLoadTestPrototypes = true };

    [Test]
    public async Task GhostJobMetadataTravelsOverNetworkAndSelectionWarps()
    {
        var map = await Pair.CreateTestMap();
        await Server.AddDummySession("OrbitraTarget");
        EntityUid target = default;
        EntityUid ghost = default;
        NetEntity netTarget = default;
        GhostWarpsResponseEvent? response = null;
        await Server.WaitPost(() =>
        {
            var minds = Server.System<MindSystem>();
            var dummy = Server.PlayerMan.Sessions.Single(s => s.Name == "OrbitraTarget");
            target = SEntMan.SpawnEntity("MobHuman", new MapCoordinates(500, 500, map.MapId));
            SEntMan.System<MetaDataSystem>().SetEntityName(target, "Ada (literal parentheses)");
            var targetMind = minds.CreateMind(dummy.UserId);
            minds.TransferTo(targetMind, target);
            Server.System<RoleSystem>().MindAddJobRole(targetMind, jobPrototype: "ChiefEngineer");
            ghost = SEntMan.SpawnEntity("MobObserver", map.MapCoords);
            var observerMind = minds.CreateMind(ServerSession!.UserId);
            minds.TransferTo(observerMind, ghost);
            netTarget = SEntMan.GetNetEntity(target);
        });
        await Pair.RunTicksSync(20);
        void Receive(GhostWarpsResponseEvent message) => response = message;
        await Client.WaitPost(() =>
        {
            var system = Client.System<Content.Client.Ghost.GhostSystem>();
            system.GhostWarpsResponse += Receive;
            system.RequestWarps();
        });
        await Pair.RunTicksSync(20);
        Assert.That(response, Is.Not.Null);
        var warp = response!.Warps.Single(w => w.Entity == netTarget);
        Assert.That(warp.CharacterName, Is.EqualTo("Ada (literal parentheses)"));
        Assert.That(warp.Job?.Id, Is.EqualTo("ChiefEngineer"));
        await Client.WaitPost(() =>
        {
            Client.System<Content.Client.Ghost.GhostSystem>().GhostWarpsResponse -= Receive;
            Client.Resolve<IEntityNetworkManager>().SendSystemNetworkMessage(new GhostWarpToTargetRequestEvent(warp.Entity));
        });
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<TransformComponent>(ghost).ParentUid, Is.EqualTo(target)));
    }

    [Test]
    public async Task OptionsAllTabsKeepControlsAndValuesAcrossResize()
    {
        await Pair.Client.WaitPost(() =>
        {
            using var window = new OptionsMenu();
            window.OpenCentered();
            var tabs = window.FindControl<TabContainer>("Tabs");
            var controls = Descendants(window).OfType<CheckBox>().ToArray();
            var states = controls.Select(c => c.Pressed).ToArray();
            for (var tab = 0; tab < tabs.ChildCount; tab++)
            {
                // Проверяем админскую форму отдельно, не меняя штатную проверку доступа окна.
                tabs.SetTabVisible(tab, true);
                tabs.CurrentTab = tab;
                foreach (var width in new[] { 480, 960, 720, 480, 960 })
                {
                    window.SetSize = new Vector2(width, 700);
                    window.Measure(window.SetSize);
                    window.Arrange(UIBox2.FromDimensions(Vector2.Zero, window.SetSize));
                    foreach (var row in Descendants(window).OfType<OrbitraFormRow>().Where(r => r.VisibleInTree))
                    {
                        Assert.That(row.Width, Is.LessThanOrEqualTo(720));
                        Assert.That(row.Orientation, Is.EqualTo(row.Width < 560 ? BoxContainer.LayoutOrientation.Vertical : BoxContainer.LayoutOrientation.Horizontal));
                        foreach (var child in row.Children.Where(c => c.Visible))
                            Assert.That(child.Position.X + child.Width, Is.LessThanOrEqualTo(row.Width + 1));
                    }
                    foreach (var check in controls.Where(c => c.VisibleInTree && !string.IsNullOrEmpty(c.Text)))
                        Assert.That(check.Label.Width, Is.GreaterThan(100));
                }
            }
            Assert.That(Descendants(window).OfType<CheckBox>(), Is.EqualTo(controls));
            Assert.That(controls.Select(c => c.Pressed), Is.EqualTo(states));
        });
    }

    [Test]
    public async Task GuideMultipleParentsKeepSelectedPathAndStopCycles()
    {
        await Client.WaitPost(() =>
        {
            using var guide = new GuidebookWindow();
            var source = CProtoMan.Index<GuideEntryPrototype>("NewPlayer");
            GuideEntry Entry(string id, params string[] children) => new()
            {
                Id = id, Name = id == "OrbitraTestA" ? "department-Engineering" : id == "OrbitraTestB" ? "department-Command" : source.Name, Text = source.Text,
                Children = children.Select(c => new ProtoId<GuideEntryPrototype>(c)).ToList()
            };
            guide.UpdateGuides(new Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry>
            {
                ["OrbitraTestA"] = Entry("OrbitraTestA", "OrbitraTestC"),
                ["OrbitraTestB"] = Entry("OrbitraTestB", "OrbitraTestC"),
                ["OrbitraTestC"] = Entry("OrbitraTestC", "OrbitraTestA")
            }, rootEntries: new List<ProtoId<GuideEntryPrototype>> { "OrbitraTestA", "OrbitraTestB" });
            guide.OpenCentered();
            var matches = guide.Tree.Items.Where(t => t.Metadata is GuideEntry { Id: "OrbitraTestC" }).ToArray();
            Assert.That(matches, Has.Length.EqualTo(2));
            Assert.That(guide.Tree.Items.Count, Is.LessThan(8));
            guide.Tree.SetSelectedIndex(matches[0].Index);
            var previousTitle = guide.FindControl<WrapContainer>("OrbitraBreadcrumbs").Children.OfType<Button>().First().Text;
            guide.Tree.SetSelectedIndex(matches[1].Index);
            Assert.That(guide.Selected?.Id, Is.EqualTo("OrbitraTestC"));
            Assert.That(guide.FindControl<WrapContainer>("OrbitraBreadcrumbs").ChildCount, Is.EqualTo(3));
            Assert.That(guide.Tree.SelectedItem, Is.SameAs(matches[1]));
            Assert.That(guide.FindControl<WrapContainer>("OrbitraBreadcrumbs").Children.OfType<Button>().First().Text, Is.Not.EqualTo(previousTitle));
        });
    }

    [Test]
    public async Task GuideSearchOnlyIndexesCurrentTreeAndKeepsSelection()
    {
        GuidebookWindow guide = null!;
        await Pair.Client.WaitPost(() =>
        {
            guide = new GuidebookWindow();
            var prototype = Pair.Client.Resolve<IPrototypeManager>().Index<GuideEntryPrototype>("NewPlayer");
            guide.UpdateGuides(new Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry> { ["NewPlayer"] = prototype }, selected: "NewPlayer");
            guide.OpenCentered();
            guide.SetSize = new Vector2(900, 600);
            guide.Measure(guide.SetSize);
            guide.Arrange(UIBox2.FromDimensions(Vector2.Zero, guide.SetSize));
            var search = guide.FindControl<LineEdit>("OrbitraArticleSearch");
            search.SetText(guide.Tree.Items.Single().Label.Text.ToUpperInvariant(), true);
        });
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(guide.FindControl<BoxContainer>("OrbitraSearchResults").Children.OfType<ContainerButton>().Count(), Is.EqualTo(1));
            Assert.That(guide.Tree.Visible, Is.False);
            Assert.That(guide.FindControl<WrapContainer>("OrbitraBreadcrumbs").ChildCount, Is.EqualTo(1));
        });
        await Pair.Client.WaitPost(() => guide.FindControl<LineEdit>("OrbitraArticleSearch").SetText("no-such-article-01928", true));
        await Pair.Client.WaitAssertion(() => Assert.That(guide.FindControl<BoxContainer>("OrbitraSearchResults").Children.OfType<ContainerButton>(), Is.Empty));
        await Pair.Client.WaitPost(() => guide.FindControl<LineEdit>("OrbitraArticleSearch").SetText("", true));
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(guide.Tree.Visible, Is.True);
            Assert.That(guide.Selected?.Id, Is.EqualTo("NewPlayer"));
            Assert.That(guide.Tree.Items.Count, Is.EqualTo(1));
        });
        await Pair.Client.WaitPost(guide.Dispose);
    }

    [Test]
    public async Task GhostTargetsReuseRowsAndSearchRestoresCollapsedGroupsOutsidePvs()
    {
        GhostTargetWindow window = null!;
        ContainerButton target = null!;
        Button header = null!;
        NetEntity? selected = null;
        var warps = new[]
        {
            new GhostWarp(new NetEntity(12345601), "legacy", false) { CharacterName = "Ada", Job = "ChiefEngineer" },
            new GhostWarp(new NetEntity(12345602), "legacy", false) { CharacterName = "Bob", Job = "Captain" },
            new GhostWarp(new NetEntity(12345603), "legacy", false) { CharacterName = "Unknown" },
            new GhostWarp(new NetEntity(12345604), "Bridge", true)
        };
        await Pair.Client.WaitPost(() =>
        {
            window = new GhostTargetWindow();
            window.UpdateWarps(warps);
            window.WarpClicked += id => selected = id;
            window.OpenCentered();
            window.SetSize = new Vector2(600, 700);
            window.Measure(window.SetSize);
            window.Arrange(UIBox2.FromDimensions(Vector2.Zero, window.SetSize));
            target = Descendants(window).OfType<ContainerButton>().Single(c => c.ToolTip?.StartsWith("Ada\n") == true);
            header = (Button)target.Parent!.Parent!.GetChild(0);
        });
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(header.Text, Does.Contain(Pair.Client.Resolve<Robust.Shared.Localization.ILocalizationManager>().GetString("department-Engineering")));
            Assert.That(Descendants(target).OfType<TextureRect>().Single().Texture, Is.Not.Null);
            Assert.That(target.VisibleInTree, Is.True);
        });
        await Click(target);
        Assert.That(selected, Is.EqualTo(warps[0].Entity));
        await Click(header);
        await Pair.Client.WaitAssertion(() => Assert.That(target.VisibleInTree, Is.False));
        await Pair.Client.WaitPost(() =>
        {
            window.UpdateWarps(warps.Reverse());
            window.FindControl<LineEdit>("SearchBar").SetText("Ada", true);
        });
        await Pair.Client.WaitAssertion(() => Assert.That(target.VisibleInTree, Is.True));
        await Pair.Client.WaitPost(() =>
        {
            window.FindControl<LineEdit>("SearchBar").SetText("", true);
            window.Close();
            window.OpenCentered();
        });
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(target.VisibleInTree, Is.False);
            Assert.That(Descendants(window).OfType<ContainerButton>().Single(c => c.ToolTip?.StartsWith("Ada\n") == true), Is.SameAs(target));
        });
        await Pair.Client.WaitPost(window.Dispose);
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
