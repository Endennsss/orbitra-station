using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Examine;
using Content.Client.Strip;
using Content.Client._Orbitra.UserInterface;
using Content.IntegrationTests.Fixtures;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraInspectionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true, NoLoadTestPrototypes = true };

    private static IEnumerable<Control> All(Control control)
    {
        yield return control;
        foreach (var child in control.Children)
        foreach (var nested in All(child))
            yield return nested;
    }

    [Test]
    public async Task ExamineUsesOpaqueSurfaceAndWrapsLongName()
    {
        EntityUid target = default;
        PanelContainer panel = null!;
        var ui = Client.Resolve<IUserInterfaceManager>();
        await Client.WaitPost(() =>
        {
            target = CEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            CEntMan.System<MetaDataSystem>().SetEntityName(target,
                "A very long character name that must wrap without overlapping the description below it");
            var examine = CEntMan.System<ExamineSystem>();
            examine.SendExamineTooltip(target, target, FormattedMessage.FromUnformatted("Description under the heading"), false, true);
            panel = All(ui.ModalRoot).OfType<PanelContainer>().Single(p => p.Name == "ExaminePopupPanel");
        });
        try
        {
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() =>
            {
                Assert.That(panel.HasStyleClass("OrbitraTooltip"), Is.True);
                Assert.That(panel.ModulateSelfOverride, Is.EqualTo(Color.White));
                Assert.That(panel.TryGetStyleProperty<StyleBox>(PanelContainer.StylePropertyPanel, out var box), Is.True);
                Assert.That(((StyleBoxFlat) box).BackgroundColor.A, Is.EqualTo(1));
                Assert.That(panel.Width, Is.LessThanOrEqualTo(400));
                var content = panel.GetChild(0);
                Assert.That(content.GetChild(1).Position.Y,
                    Is.GreaterThanOrEqualTo(content.GetChild(0).Position.Y + content.GetChild(0).Height));
            });
        }
        finally
        {
            await Client.WaitPost(() =>
            {
                ((Popup) panel.Parent!).Close();
                CEntMan.DeleteEntity(target);
            });
        }
    }

    [Test]
    public async Task StrippingKeepsUserSizeAndStylesNewActions()
    {
        StrippingMenu window = null!;
        Button action = null!;
        await Client.WaitPost(() =>
        {
            window = new StrippingMenu { Title = "Long inventory title for testing" };
            window.OpenCentered();
            var slot = new Control { SetSize = new Vector2(64) };
            LayoutContainer.SetPosition(slot, new Vector2(136, 408));
            window.InventoryContainer.AddChild(slot);
            window.ApplyOrbitraLayout(new Vector2(240, 700));
            window.SetSize = new Vector2(340, 400);
            action = new Button { Text = "Admin view", ToggleMode = true };
            window.ButtonContainer.AddChild(action);
            window.ApplyOrbitraLayout(new Vector2(240, 700));
        });
        try
        {
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() =>
            {
                Assert.That(window.HasStyleClass("OrbitraEntryWindow"), Is.True);
                Assert.That(window.SetSize, Is.EqualTo(new Vector2(340, 400)));
                Assert.That(action.HasStyleClass("OrbitraEditorControl"), Is.True);
                Assert.That(action.ToggleMode, Is.True);
                Assert.That(window.InventoryContainer.MinSize, Is.EqualTo(new Vector2(200, 472)));
                Assert.That(All(window).OfType<ScrollContainer>().Count(), Is.EqualTo(1));
                Assert.That(All(window).OfType<OrbitraWindowCloseButton>().Single().Size, Is.EqualTo(new Vector2(32)));
            });
        }
        finally
        {
            await Client.WaitPost(() => window.Dispose());
        }
    }
}
