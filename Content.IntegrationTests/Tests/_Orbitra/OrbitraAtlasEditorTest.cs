using System.Collections.Generic;
using System.Linq;
using Content.Client._Orbitra.Lobby.Roadmap;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Orbitra.Roadmap;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraAtlasEditorTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true };

    [Test]
    public async Task ConflictingSavePreservesTextAndTaskOrderUntilExplicitRetry()
    {
        var id = Guid.NewGuid();
        var draft = new AtlasContent("Release", "", AtlasStatus.Active,
            [new AtlasTask("First", AtlasStatus.Done), new AtlasTask("Second", AtlasStatus.Planned)]);
        var entry = new AtlasEntry(id, 3, draft, false, true);
        var other = entry with { Revision = 4, Content = draft with { Title = "Another editor's title" } };
        var changes = new List<AtlasChange>();
        OrbitraAtlasEditorWindow editor = null!;
        await Client.WaitPost(() =>
        {
            editor = new OrbitraAtlasEditorWindow(id, entry);
            editor.Submitted += changes.Add;
            editor.Display(new OrbitraRoadmapState { CanEdit = true, Entries = [entry] });
            editor.OpenCentered();
        });
        try
        {
            await Pair.RunTicksSync(2);
            var title = editor.FindControl<LineEdit>("EntryTitle");
            var description = editor.FindControl<TextEdit>("EntryDescription");
            var tasks = editor.FindControl<BoxContainer>("TaskList");
            var save = editor.FindControl<Button>("Save");
            var publish = editor.FindControl<Button>("Publish");
            var keep = editor.FindControl<Button>("KeepForm");
            var conflict = editor.FindControl<BoxContainer>("ConflictPanel");

            var fields = (BoxContainer) tasks.GetChild(0).GetChild(0);
            var actions = fields.Children.OfType<WrapContainer>().Single();
            await Client.WaitPost(() =>
            {
                editor.SetWidth = 400;
                OrbitraWindowLifecycleTest.Frame(Client.Resolve<IUserInterfaceManager>(), 0);
                title.CursorPosition = title.Text.Length;
                title.InsertAtCursor(" candidate");
            });
            await Client.WaitAssertion(() =>
            {
                Assert.That(fields.Orientation, Is.EqualTo(BoxContainer.LayoutOrientation.Vertical));
                Assert.That(editor.FindControl<OptionButton>("EntryStatus").Width, Is.GreaterThanOrEqualTo(160));
                Assert.That(actions.Children.OfType<OptionButton>().Single().Width, Is.GreaterThanOrEqualTo(160));
            });
            await Click(actions.Children.OfType<Button>().ElementAt(1));
            await Click(save);
            await Client.WaitAssertion(() =>
            {
                Assert.That(changes.Count, Is.EqualTo(1));
                Assert.That(changes[0].Revision, Is.EqualTo(3));
                Assert.That(changes[0].Content!.Tasks, Is.EqualTo(draft.Tasks.Reverse()));
            });
            await Client.WaitPost(() =>
            {
                description.InsertAtCursor("While waiting\nSecond line");
                editor.Display(new OrbitraRoadmapState
                {
                    CanEdit = true, Entries = [other], ReplyTo = Guid.NewGuid(), Success = true,
                });
            });
            await Client.WaitAssertion(() =>
            {
                Assert.That(save.Disabled, Is.True);
                Assert.That(conflict.Visible, Is.False);
                Assert.That(title.Text, Is.EqualTo("Release candidate"));
            });
            await Client.WaitPost(() => editor.Display(new OrbitraRoadmapState
            {
                CanEdit = true, Entries = [other], ReplyTo = changes[0].RequestId, Notice = "orbitra-atlas-conflict",
            }));
            await Client.WaitAssertion(() =>
            {
                Assert.That(conflict.Visible, Is.True);
                Assert.That(publish.Disabled, Is.True);
            });
            await Click(keep);
            await Client.WaitAssertion(() =>
            {
                Assert.That(conflict.Visible, Is.False);
                Assert.That(publish.Disabled, Is.False);
            });
            await Click(publish);
            await Client.WaitAssertion(() =>
            {
                Assert.That(changes.Count, Is.EqualTo(2));
                var retry = changes[1];
                Assert.That(retry.Action, Is.EqualTo(AtlasAction.Publish));
                Assert.That(retry.Id, Is.EqualTo(id));
                Assert.That(retry.RequestId, Is.Not.EqualTo(changes[0].RequestId));
                Assert.That(retry.Revision, Is.EqualTo(4));
                Assert.That(retry.Content!.Title, Is.EqualTo("Release candidate"));
                Assert.That(retry.Content.Description, Is.EqualTo("While waiting\nSecond line"));
                Assert.That(retry.Content.Tasks, Is.EqualTo(draft.Tasks.Reverse()));
            });
            await Client.WaitPost(() =>
            {
                title.Text = "Further local edits";
                editor.Display(new OrbitraRoadmapState
                {
                    CanEdit = true, ReplyTo = changes[1].RequestId, Success = true,
                    Entries = [new AtlasEntry(id, 5, changes[1].Content!, true, false)],
                });
            });
            await Click(save);
            await Client.WaitAssertion(() =>
            {
                Assert.That(changes.Count, Is.EqualTo(3));
                Assert.That(changes[2].Revision, Is.EqualTo(5));
                Assert.That(changes[2].Content!.Title, Is.EqualTo("Further local edits"));
            });
        }
        finally
        {
            await Client.WaitPost(editor.Close);
        }
    }

    [Test]
    public async Task RevokingEditorClosesPrivateFormAndReopeningLoadsSavedDraft()
    {
        var draft = new AtlasContent("Unpublished", "Saved draft", AtlasStatus.Planned, []);
        var entry = new AtlasEntry(Guid.NewGuid(), 6, draft, false, true);
        var editable = new OrbitraRoadmapState { CanEdit = true, Entries = [entry] };
        var windows = Client.Resolve<IUserInterfaceManager>().WindowRoot;
        OrbitraAtlasEui eui = null!;
        await Client.WaitPost(() =>
        {
            eui = new OrbitraAtlasEui();
            eui.Opened();
            eui.HandleState(editable);
        });
        try
        {
            await Pair.RunTicksSync(2);
            var atlas = windows.Children.OfType<OrbitraAtlasWindow>().Single();
            var edit = atlas.FindControl<Button>("EditButton");
            await Click(edit);
            var first = windows.Children.OfType<OrbitraAtlasEditorWindow>().Single();
            await Client.WaitPost(() =>
            {
                first.FindControl<LineEdit>("EntryTitle").Text = "Private unsaved edit";
                eui.HandleState(new OrbitraRoadmapState());
            });
            await Client.WaitAssertion(() =>
            {
                Assert.That(first.IsOpen, Is.False);
                Assert.That(windows.Children.OfType<OrbitraAtlasEditorWindow>(), Is.Empty);
                Assert.That(edit.VisibleInTree, Is.False);
            });
            await Client.WaitPost(() => eui.HandleState(editable));
            await Click(edit);
            await Client.WaitAssertion(() =>
            {
                var reopened = windows.Children.OfType<OrbitraAtlasEditorWindow>().Single();
                Assert.That(reopened.FindControl<LineEdit>("EntryTitle").Text, Is.EqualTo(draft.Title));
                Assert.That(Rope.Collapse(reopened.FindControl<TextEdit>("EntryDescription").TextRope),
                    Is.EqualTo(draft.Description));
                Assert.That(reopened.FindControl<Button>("Unpublish").VisibleInTree, Is.False);
            });
        }
        finally
        {
            await Client.WaitPost(eui.Closed);
        }
    }

    private async Task Click(Control control)
    {
        // После смены снимка headless-клиенту нужен UI-кадр для раскладки появившихся кнопок.
        await Client.WaitPost(() => OrbitraWindowLifecycleTest.Frame(Client.Resolve<IUserInterfaceManager>(), 0));
        await Client.WaitAssertion(() => Assert.That(control.PixelSize.X * control.PixelSize.Y, Is.Positive));
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
        {
            var screen = new ScreenCoordinates(control.GlobalPixelPosition + control.PixelSize / 2, control.Window?.Id ?? default);
            await Client.DoGuiEvent(control, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, state, screen, default,
                screen.Position / control.UIScale - control.GlobalPosition, screen.Position - control.GlobalPixelPosition));
            await Pair.RunTicksSync(1);
        }
    }
}
