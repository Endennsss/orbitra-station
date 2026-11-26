#nullable enable
using System;
using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.Surgery;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra.Surgery;

[TestFixture, NonParallelizable]
public sealed class OrbitraSurgeryLayoutTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task PatientSidesAndFooterStayAccessibleAtBothScales()
    {
        var map = await Pair.CreateTestMap();
        NetEntity patient = default;
        await Server.WaitPost(() =>
        {
            var actor = SSpawnAtPosition("MobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, actor);
            var tool = SSpawnAtPosition("OrbitraBoneSetter", map.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickup(actor, tool), Is.True);
            patient = SEntMan.GetNetEntity(SSpawnAtPosition("MobHuman", map.GridCoords));
        });
        await Pair.RunTicksSync(12);
        var config = Client.ResolveDependency<IConfigurationManager>();
        var originalScale = config.GetCVar(CVars.DisplayUIScale);
        var autoScale = config.GetCVar(CVars.ResAutoScaleEnabled);
        var ui = Client.ResolveDependency<IUserInterfaceManager>();
        var injuries = Enum.GetValues<SurgeryZone>().ToDictionary(zone => zone, _ => SurgeryInjury.None);
        injuries[SurgeryZone.LeftArm] = SurgeryInjury.Missing;
        injuries[SurgeryZone.RightArm] = SurgeryInjury.Fracture;
        var idle = new OrbitraSurgeryState(patient, "Urist", injuries, null, null, 0, false);
        var active = new OrbitraSurgeryState(patient, "Urist", injuries, SurgeryZone.RightArm, SurgeryProcedure.Fracture, 1, false);
        OrbitraSurgeryWindow? window = null;
        try
        {
            await Client.WaitPost(() =>
            {
                config.SetCVar(CVars.ResAutoScaleEnabled, false);
                config.SetCVar(CVars.DisplayUIScale, 1f);
                window = new OrbitraSurgeryWindow();
                window.OpenCentered();
                window.UpdateState(new OrbitraSurgeryState(patient, "Urist", [], null, null, 0, false));
            });
            await Client.WaitAssertion(() =>
            {
                Assert.That(window!.FindControl<BoxContainer>("Columns").Visible, Is.False);
                Assert.That(window.FindControl<Label>("PatientSummary").Visible, Is.False);
                Assert.That(window.FindControl<RichTextLabel>("Status").GetMessage(),
                    Is.EqualTo(Client.ResolveDependency<ILocalizationManager>().GetString("orbitra-surgery-unsupported")));
            });
            await Client.WaitPost(() => window!.UpdateState(idle));
            await DrawFrames();
            var body = window!.FindControl<OrbitraSurgeryBodyMap>("BodyMap");
            await Client.WaitAssertion(() =>
            {
                var grid = window.FindControl<GridContainer>("ZoneButtons");
                var buttons = grid.Children.OfType<Button>().ToArray();
                Assert.That(buttons, Has.Length.EqualTo(6));
                foreach (var button in buttons)
                {
                    var point = button.GlobalPixelPosition + button.PixelSize / 2;
                    Assert.That(ui.MouseGetControl(new ScreenCoordinates(point, button.Window!.Id)), Is.SameAs(button),
                        $"Область должна быть доступна до прокрутки: {button.Text}");
                }
            });
            foreach (var (zone, horizontal) in new[] { (SurgeryZone.RightArm, -7.5f), (SurgeryZone.LeftArm, 6.5f) })
            {
                var zoom = MathF.Floor((body.PixelHeight - 12 * body.UIScale) / 32);
                var pixel = (Vector2) body.PixelSize / 2 + new Vector2(horizontal, 1.5f) * zoom;
                await Click(body, pixel);
                await Client.WaitAssertion(() => Assert.That(body.Selected, Is.EqualTo(zone)));
            }
            OrbitraSurgeryStepMessage? requested = null;
            OrbitraSurgeryCancelMessage? cancelled = null;
            await Client.WaitPost(() =>
            {
                window.StepRequested += message => requested = message;
                window.CancelRequested += message => cancelled = message;
                window.UpdateState(active);
            });
            foreach (var scale in new[] { 1f, 1.8f })
            {
                await Client.WaitPost(() => config.SetCVar(CVars.DisplayUIScale, scale));
                await DrawFrames();
                var action = window.FindControl<Button>("ActionButton");
                var cancel = window.FindControl<Button>("CancelButton");
                await Client.WaitAssertion(() =>
                {
                    Assert.That(window.UIScale, Is.EqualTo(scale).Within(0.01f));
                    Assert.That(window.FindControl<BoxContainer>("Columns").Orientation, Is.EqualTo(scale == 1
                        ? BoxContainer.LayoutOrientation.Horizontal : BoxContainer.LayoutOrientation.Vertical));
                    foreach (var button in new[] { action, cancel })
                    {
                        var point = button.GlobalPixelPosition + button.PixelSize / 2;
                        Assert.That(point.X, Is.InRange(0, 1279));
                        Assert.That(point.Y, Is.InRange(0, 719));
                        Assert.That(button.VisibleInTree && button.CanKeyboardFocus && !button.Disabled, Is.True, button.Name);
                        Assert.That(ui.MouseGetControl(new ScreenCoordinates(point, button.Window!.Id)), Is.SameAs(button), button.Name);
                    }
                });
                await Click(action, (Vector2) action.PixelSize / 2);
                await Click(cancel, (Vector2) cancel.PixelSize / 2);
                await Client.WaitAssertion(() =>
                {
                    Assert.That(requested?.Zone, Is.EqualTo(SurgeryZone.RightArm));
                    Assert.That(requested?.Step, Is.EqualTo(1));
                    Assert.That(cancelled?.Patient, Is.EqualTo(patient));
                    Assert.That(cancelled?.Step, Is.EqualTo(1));
                });
                requested = null;
                cancelled = null;
            }
        }
        finally
        {
            await Client.WaitPost(() =>
            {
                window?.Close();
                config.SetCVar(CVars.ResAutoScaleEnabled, autoScale);
                config.SetCVar(CVars.DisplayUIScale, originalScale);
            });
        }

        async Task DrawFrames()
        {
            await Client.WaitPost(() =>
            {
                for (var frame = 0; frame < 12; frame++)
                    OrbitraWindowLifecycleTest.Frame(ui, 0.025f);
            });
        }

        async Task Click(Control control, Vector2 pixel)
        {
            var screen = new ScreenCoordinates(control.GlobalPixelPosition + pixel, control.Window!.Id);
            foreach (var phase in new[] { BoundKeyState.Down, BoundKeyState.Up })
                await Client.DoGuiEvent(control, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, phase,
                    screen, false, pixel / control.UIScale, pixel));
        }
    }
}
