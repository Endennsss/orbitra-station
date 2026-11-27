#nullable enable
using System;
using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.Surgery;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Orbitra.Surgery;
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
        injuries[SurgeryZone.RightLeg] = SurgeryInjury.Shrapnel;
        injuries[SurgeryZone.Head] = SurgeryInjury.Shrapnel;
        var loc = Client.ResolveDependency<ILocalizationManager>();
        var idle = new OrbitraSurgeryState(patient, "Urist", injuries, null, null, 0, false)
        {
            Access = { [SurgeryZone.RightArm] = SurgeryAccess.Open, [SurgeryZone.Head] = SurgeryAccess.Open },
            Organs = { [SurgeryOrgan.Eyes] = SurgeryOrganCondition.Damaged },
        };
        var active = new OrbitraSurgeryState(patient, "Urist", injuries, SurgeryZone.RightArm, SurgeryProcedure.Fracture, 1, false)
        {
            Access = { [SurgeryZone.RightArm] = SurgeryAccess.Open },
        };
        OrbitraSurgeryWindow? window = null;
        try
        {
            await Client.WaitPost(() =>
            {
                config.SetCVar(CVars.ResAutoScaleEnabled, false);
                config.SetCVar(CVars.DisplayUIScale, 1f);
                window = new OrbitraSurgeryWindow();
                window.OpenCenteredLeft();
                window.UpdateState(new OrbitraSurgeryState(patient, "Urist", [], null, null, 0, false));
            });
            await Client.WaitAssertion(() =>
            {
                Assert.That(window!.FindControl<BoxContainer>("Columns").Visible, Is.False);
                Assert.That(window.FindControl<Label>("PatientSummary").Visible, Is.False);
                Assert.That(window.FindControl<RichTextLabel>("Status").GetMessage(),
                    Is.EqualTo(loc.GetString("orbitra-surgery-unsupported")));
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
            int deselections = 0;
            var procedures = window.FindControl<BoxContainer>("ProcedureButtons");
            var zones = window.FindControl<GridContainer>("ZoneButtons");
            var collapse = window.FindControl<Button>("CollapseButton");
            var confirm = window.FindControl<Button>("ConfirmButton");
            var status = window.FindControl<RichTextLabel>("Status");
            await Client.WaitPost(() =>
            {
                window.TaskSelected += message => requested = message;
                window.TaskDeselected += _ => deselections++;
                window.CancelRequested += message => cancelled = message;
            });

            var head = zones.Children.OfType<Button>().ElementAt(0);
            var rightArm = zones.Children.OfType<Button>().ElementAt(2);
            var rightLeg = zones.Children.OfType<Button>().ElementAt(4);
            var evidence = window.FindControl<RichTextLabel>("EvidenceHint");
            await Press(rightLeg);
            await DrawFrames();
            await Client.WaitAssertion(() =>
            {
                Assert.That(evidence.VisibleInTree, Is.True);
                Assert.That(evidence.GetMessage(), Is.EqualTo(loc.GetString("orbitra-surgery-fragment-access")));
                Assert.That(rightLeg.Label.Width + 1, Is.GreaterThanOrEqualTo(rightLeg.Label.DesiredSize.X));
            });
            await Client.WaitPost(() =>
            {
                idle.Access[SurgeryZone.RightLeg] = SurgeryAccess.Open;
                window.UpdateState(idle);
            });
            await Client.WaitAssertion(() =>
                Assert.That(evidence.GetMessage(), Is.EqualTo(loc.GetString("orbitra-surgery-fragment-ready"))));
            await Press(rightArm);
            await Client.WaitAssertion(() => Assert.That(evidence.Visible, Is.False));
            await Press(head);
            var eye = window.FindControl<BoxContainer>("OrganButtons").Children.OfType<Button>().Single(button => button.Visible);
            await Press(eye);
            await Client.WaitAssertion(() => Assert.That(Procedure("shrapnel").VisibleInTree, Is.True));
            await Press(Procedure("extractorgan"));
            await Client.WaitAssertion(() =>
            {
                Assert.That(confirm.VisibleInTree, Is.True);
                Assert.That(requested, Is.Null);
                Assert.That(status.GetMessage(), Does.Contain("Urist").And.Contain(loc.GetString("orbitra-surgery-organ-eyes")));
            });
            await Press(confirm);
            await Client.WaitAssertion(() =>
            {
                Assert.That(requested?.Organ, Is.EqualTo(SurgeryOrgan.Eyes));
                Assert.That(requested?.Confirmed, Is.True);
            });
            requested = null;
            await Press(rightArm);
            await Press(Procedure("amputate"));
            await Client.WaitAssertion(() =>
            {
                Assert.That(deselections, Is.Positive);
                Assert.That(requested, Is.Null);
                Assert.That(status.GetMessage(), Does.Contain("Urist").And.Contain(loc.GetString("orbitra-surgery-zone-rightarm")));
            });
            var cancel = window.FindControl<Button>("CancelButton");
            await Press(cancel);
            await Client.WaitAssertion(() => Assert.That(confirm.Visible, Is.False));
            await Client.WaitPost(() => window.UpdateState(active));

            foreach (var scale in new[] { 1f, 1.8f })
            {
                await Client.WaitPost(() => config.SetCVar(CVars.DisplayUIScale, scale));
                await DrawFrames();
                await Press(Procedure("fracture"));
                var selection = window.FindControl<RichTextLabel>("SelectionSummary");
                var tool = window.FindControl<RichTextLabel>("RequiredTool");
                var details = window.FindControl<ScrollContainer>("Details");
                var progress = window.FindControl<ProgressBar>("Progress");
                await Client.WaitAssertion(() =>
                {
                    Assert.That(window.UIScale, Is.EqualTo(scale).Within(0.01f));
                    Assert.That(requested?.Zone, Is.EqualTo(SurgeryZone.RightArm));
                    Assert.That(requested?.Procedure, Is.EqualTo(SurgeryProcedure.Fracture));
                    Assert.That(requested?.Step, Is.EqualTo(1));
                    Assert.That(Procedure("fracture").Text, Does.StartWith("▸"));
                    Assert.That(selection.GetMessage(), Does.Contain(loc.GetString("orbitra-surgery-access-open")));
                    Assert.That(details.VisibleInTree, Is.True);
                });

                var before = deselections;
                var expanded = window.Size;
                await Press(collapse);
                await DrawFrames();
                await Client.WaitAssertion(() =>
                {
                    Assert.That(window.IsOpen && selection.VisibleInTree && tool.VisibleInTree && progress.VisibleInTree, Is.True);
                    Assert.That(details.VisibleInTree, Is.False);
                    Assert.That(window.Width, Is.LessThan(expanded.X));
                    Assert.That(window.Height, Is.LessThan(expanded.Y));
                    Assert.That(progress.Value, Is.GreaterThan(0f));
                    Assert.That(selection.GetMessage(), Does.Contain(loc.GetString("orbitra-surgery-procedure-fracture")));
                    Assert.That(deselections, Is.EqualTo(before));
                    foreach (var button in new[] { collapse, cancel })
                    {
                        var point = button.GlobalPixelPosition + button.PixelSize / 2;
                        Assert.That(button.VisibleInTree && button.CanKeyboardFocus && !button.Disabled, Is.True, button.Name);
                        Assert.That(ui.MouseGetControl(new ScreenCoordinates(point, button.Window!.Id)), Is.SameAs(button), button.Name);
                    }
                });
                await Client.WaitPost(() => window.UpdateState(new OrbitraSurgeryState(patient, "Urist", injuries,
                    SurgeryZone.Head, SurgeryProcedure.ExtractOrgan, 1, true, organs: idle.Organs, activeOrgan: SurgeryOrgan.Eyes)
                {
                    Access = idle.Access,
                }));
                await Client.WaitAssertion(() =>
                {
                    Assert.That(selection.GetMessage(), Does.Contain(loc.GetString("orbitra-surgery-procedure-fracture")));
                    Assert.That(status.GetMessage(), Does.Contain(loc.GetString("orbitra-surgery-procedure-extractorgan")));
                    Assert.That(deselections, Is.EqualTo(before));
                    Assert.That(cancel.Visible, Is.False);
                });
                await Client.WaitPost(() => window.UpdateState(active));
                await Press(cancel);
                await Press(collapse);
                await DrawFrames();
                await Client.WaitAssertion(() =>
                {
                    Assert.That(details.VisibleInTree, Is.True);
                    Assert.That(cancelled?.Patient, Is.EqualTo(patient));
                    Assert.That(cancelled?.Step, Is.EqualTo(1));
                });
                requested = null;
                cancelled = null;
            }
            Button Procedure(string key)
            {
                var name = loc.GetString($"orbitra-surgery-procedure-{key}");
                return procedures.Children.OfType<Button>().Single(button => button.ToolTip == name);
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

        async Task Press(Button button)
        {
            await DrawFrames();
            await Click(button, (Vector2) button.PixelSize / 2);
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
