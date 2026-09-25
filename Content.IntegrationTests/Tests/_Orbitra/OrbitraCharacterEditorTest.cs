using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Humanoid;
using Content.Client.Lobby.UI.Roles;
using Content.Client.Options.UI;
using Content.Client._Orbitra.UserInterface;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraCharacterEditorTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true, NoLoadTestPrototypes = true };

    private static IEnumerable<Control> All(Control root)
    {
        yield return root;
        foreach (var child in root.Children)
        foreach (var nested in All(child))
            yield return nested;
    }

    [Test]
    public async Task HumanMarkingsLoadAfterOrganDataAndAfterReopening()
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.Resolve<IUserInterfaceManager>();
            var manager = Client.Resolve<MarkingManager>();
            var model = new MarkingsViewModel();
            var picker = new MarkingPicker();
            try
            {
                picker.SetModel(model);
                OrbitraEditorStyles.Apply(picker);
                ui.StateRoot.AddChild(picker);
                model.OrganData = manager.GetMarkingData("Human");
                model.OrganProfileData = manager.GetProfileData("Human", Sex.Male, Color.White, Color.Blue);
                Assert.That(All(picker).OfType<LayerMarkingItem>().Any(i => i.MarkingId.Id.StartsWith("HumanHair")), Is.True);
                var count = All(picker).OfType<LayerMarkingItem>().Count();
                var first = All(picker).OfType<OrganMarkingPicker>().First();
                var oldLayers = first.FindControl<TabContainer>("LayerTabs").Children.ToArray();
                OrbitraWindowLifecycleTest.Frame(ui, 0);
                model.OrganProfileData = manager.GetProfileData("Human", Sex.Male, Color.White, Color.Blue);
                Assert.That(first.IsInsideTree, Is.False);
                Assert.That(first.FindControl<TabContainer>("LayerTabs").Children, Is.EqualTo(oldLayers),
                    "A removed tab must not rebuild from the remaining callbacks of the same model event.");
                first = All(picker).OfType<OrganMarkingPicker>().First();
                OrbitraWindowLifecycleTest.Frame(ui, 0);
                picker.Orphan();
                model.OrganData = manager.GetMarkingData("Human");
                Assert.That(All(picker).OfType<OrganMarkingPicker>().First(), Is.SameAs(first), "Detached picker must unsubscribe.");
                ui.StateRoot.AddChild(picker);
                Assert.That(All(picker).OfType<LayerMarkingItem>().Count(), Is.EqualTo(count));
                OrbitraWindowLifecycleTest.Frame(ui, 0);
                var replacement = new MarkingsViewModel
                {
                    OrganProfileData = manager.GetProfileData("Human", Sex.Male, Color.White, Color.Blue),
                    OrganData = manager.GetMarkingData("Human"),
                };
                picker.SetModel(replacement);
                first = All(picker).OfType<OrganMarkingPicker>().First();
                model.OrganData = manager.GetMarkingData("Human");
                Assert.That(All(picker).OfType<OrganMarkingPicker>().First(), Is.SameAs(first), "Old model must unsubscribe.");
                Assert.That(All(picker).OfType<LayerMarkingItem>().Count(), Is.EqualTo(count));
            }
            finally { picker.Dispose(); }
        });
    }

    [Test]
    public async Task SettingsToastOverlaysContentAndEquipmentSharesPriorityRow()
    {
        OptionsMenu menu = null!;
        RequirementsSelector selector = null!;
        Button equipment = null!;
        await Client.WaitPost(() =>
        {
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            menu = new OptionsMenu();
            menu.OpenCentered();
            selector = new RequirementsSelector();
            selector.Setup([("humanoid-profile-editor-job-priority-never-button", 0),
                ("humanoid-profile-editor-job-priority-high-button", 1)], "Капитан", 200, "Описание");
            equipment = new Button { Text = "Снаряжение", VerticalAlignment = Control.VAlignment.Center };
            selector.AttachOrbitraEquipment(equipment);
            Client.Resolve<IUserInterfaceManager>().StateRoot.AddChild(selector);
        });
        try
        {
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() =>
            {
                var toast = All(menu).OfType<OrbitraNotification>().Single();
                Assert.That(toast.Parent, Is.Not.InstanceOf<BoxContainer>());
                var tabs = All(menu).OfType<TabContainer>().First();
                var size = tabs.Size;
                toast.Show(OrbitraNotificationKind.Success, "Применено");
                Assert.That(toast.HasMessage, Is.True);
                Assert.That(All(toast).OfType<OrbitraMotionHost>().Single().HorizontalAlignment, Is.EqualTo(Control.HAlignment.Left));
                toast.Dismiss();
                Assert.That(toast.HasMessage, Is.False);
                Assert.That(tabs.Size, Is.EqualTo(size));
                foreach (var width in new[] { 700f, 480f, 320f })
                {
                    selector.Measure(new Vector2(width, 300));
                    selector.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(width, selector.DesiredSize.Y)));
                    var priority = equipment.Parent!.GetChild(0);
                    Assert.That(equipment.Position.Y + equipment.Height / 2,
                        Is.EqualTo(priority.Position.Y + priority.Height / 2).Within(1));
                    Assert.That(equipment.Position.X + equipment.Width, Is.LessThanOrEqualTo(width + 1));
                }
            });
        }
        finally { await Client.WaitPost(() => { menu.Dispose(); selector.Dispose(); }); }
    }
}
