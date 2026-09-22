using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Content.Client._Orbitra.Lobby;
using Content.IntegrationTests.Fixtures;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

using Content.Client._Orbitra.UserInterface; // Orbitra-Edit

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraJournalUiTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true };

    [Test]
    public async Task JournalNavigationKeepsSelectionAndPermissionsAtAnyWidth()
    {
        var client = Pair.Client;
        OrbitraJournalTabs tabs = null;
        await client.WaitPost(() =>
        {
            tabs = new OrbitraJournalTabs();
            client.Resolve<IUserInterfaceManager>().StateRoot.AddChild(tabs);
            for (var i = 0; i < 5; i++) tabs.AddSection(i.ToString(), $"Long journal section {i}");
            tabs.SetAllowed("4", false);
            tabs.Select("2");
        });
        foreach (var width in new[] { 1000, 320, 500, 1000 })
        {
            await client.WaitPost(() =>
            {
                tabs.Measure(new Vector2(width, 36));
                tabs.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(width, 36)));
            });
            await client.WaitAssertion(() =>
            {
                var items = tabs.Children.OfType<ScrollContainer>().Single().Children.OfType<BoxContainer>().Single().Children.OfType<Button>().ToArray();
                Assert.That(items[2].Pressed, Is.True);
                Assert.That(items[4].Visible, Is.False);
                Assert.That(tabs.Height, Is.EqualTo(36));
                Assert.That(tabs.Children.OfType<OptionButton>(), Is.Empty);
            });
        }
        await client.WaitPost(tabs.Dispose);
    }

    [Test]
    public async Task LongJournalKeepsOnlyViewportRowsAlive()
    {
        var client = Pair.Client;
        ScrollContainer scroll = null;
        OrbitraVirtualList list = null;
        var created = 0;
        await client.WaitPost(() =>
        {
            scroll = new ScrollContainer { SetSize = new Vector2(500, 300), HScrollEnabled = false, ReserveScrollbarSpace = true };
            var rows = new List<Func<Control>>();
            for (var i = 0; i < 1000; i++)
                rows.Add(() =>
                {
                    created++;
                    var label = new RichTextLabel();
                    label.SetMessage("A long journal entry that wraps across several lines when the window becomes narrower. Details must remain readable without overlapping adjacent entries.");
                    return label;
                });
            list = new OrbitraVirtualList(scroll, rows);
            scroll.AddChild(list);
            client.Resolve<IUserInterfaceManager>().StateRoot.AddChild(scroll);
            scroll.Measure(new Vector2(500, 300));
            scroll.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(500, 300)));
        });
        await Pair.RunTicksSync(5);
        await client.WaitAssertion(() => Assert.That(list.LiveRows, Is.InRange(1, 30)));
        var before = created;
        await client.WaitPost(() => scroll.VScroll = scroll.VScrollTarget = 20000);
        await Pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            Assert.That(list.RowCount, Is.EqualTo(1000));
            Assert.That(list.LiveRows, Is.InRange(1, 30));
            Assert.That(created - before, Is.LessThan(40), "Scrolling must not remeasure all rows");
            var visible = list.Children.OrderBy(row => row.Position.Y).ToArray();
            for (var i = 1; i < visible.Length; i++)
                Assert.That(visible[i - 1].Position.Y + visible[i - 1].Height, Is.LessThanOrEqualTo(visible[i].Position.Y), "Measured row heights must prevent overlap");
        });
        await client.WaitPost(() => scroll.Dispose());
    }
}
