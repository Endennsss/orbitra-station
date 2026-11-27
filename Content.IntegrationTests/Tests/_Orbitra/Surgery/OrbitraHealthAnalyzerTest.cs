#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.Surgery;
using Content.Client.HealthAnalyzer.UI;
using Content.Client.Medical.Cryogenics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Medical.HealthAnalyzer;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Medical.Cryogenics;
using Content.Shared.MedicalScanner;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Orbitra.Surgery;

[TestFixture, NonParallelizable]
public sealed class OrbitraHealthAnalyzerTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private const string PatientName = "Александра Вадимовна Александрова-Ковалевская";
    private HealthAnalyzerWindow _window = null!;
    private HealthAnalyzerControl _analyzer = null!;
    private NetEntity _patient;
    private NetEntity _otherPatient;

    [SetUp]
    public async Task OpenAnalyzer()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            var first = SSpawnAtPosition("MobHuman", map.GridCoords);
            var second = SSpawnAtPosition("MobHuman", map.GridCoords);
            var metadata = Server.System<MetaDataSystem>();
            metadata.SetEntityName(first, PatientName);
            metadata.SetEntityName(second, PatientName);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, first);
            Server.System<DamageableSystem>().TryChangeDamage(first, new DamageSpecifier { DamageDict = { ["Blunt"] = 5 } });
            _patient = SEntMan.GetNetEntity(first);
            _otherPatient = SEntMan.GetNetEntity(second);
        });
        await RunTicksSync(1);
        await RunUntilSynced();
        await Client.WaitPost(() =>
        {
            _window = new HealthAnalyzerWindow();
            _window.OpenCentered();
            _analyzer = _window.FindControl<HealthAnalyzerControl>("HealthAnalyzer");
        });
    }

    [TearDown]
    public async Task CloseAnalyzer() => await Client.WaitPost(() => _window?.Dispose());

    [Test]
    public async Task NarrowHeaderFitsTheFullPatientNameStatusAndScanMode()
    {
        var state = new HealthAnalyzerUiState(_patient, 310f, 1f, true, false, false);
        await Client.WaitPost(() => _window.SetSize = new Vector2(330, 450));
        var name = _analyzer.FindControl<RichTextLabel>("PatientName");
        var status = _analyzer.FindControl<RichTextLabel>("StatusLabel");
        var connection = _analyzer.FindControl<RichTextLabel>("ScanModeLabel");
        var summary = _analyzer.FindControl<PanelContainer>("Summary");
        foreach (var live in new[] { true, false })
        {
            state.ScanMode = live;
            await Populate(state);
            await Client.WaitAssertion(() =>
            {
                Assert.That(Rich("PatientName"), Is.EqualTo(PatientName));
                Assert.That(Rich("StatusLabel"), Is.EqualTo(Localized("health-analyzer-window-entity-alive-text")));
                Assert.That(Rich("ScanModeLabel"), Is.EqualTo(Localized(live ? "orbitra-analyzer-live" : "orbitra-analyzer-paused")));
                Assert.That(name.GlobalRect.Bottom, Is.LessThanOrEqualTo(status.GlobalRect.Top));
                Assert.That(status.GlobalRect.Bottom, Is.LessThanOrEqualTo(connection.GlobalRect.Top));
                Assert.That(_analyzer.GlobalRect.Contains(summary.GlobalRect.TopLeft), Is.True);
                Assert.That(_analyzer.GlobalRect.Contains(summary.GlobalRect.BottomRight), Is.True);

                foreach (var label in new[] { name, status, connection })
                {
                    Assert.That(label.VisibleInTree && label.Width > 0 && label.Height > 0, Is.True, label.Name);
                    Assert.That(summary.GlobalRect.Encloses(label.GlobalRect), Is.True, label.Name);
                    // При фактической ширине должно хватить высоты для всех строк надписи.
                    label.Measure(new Vector2(label.Width, float.PositiveInfinity));
                    Assert.That(label.Height + 1, Is.GreaterThanOrEqualTo(label.DesiredSize.Y), label.Name);
                }
                name.Measure(new Vector2(float.PositiveInfinity));
                Assert.That(name.Height, Is.GreaterThan(name.DesiredSize.Y), "Длинное имя должно переноситься на несколько строк");
            });
        }
    }

    [Test]
    public async Task CryoPodClearsHealthyAnatomyOnEjectionAndShowsReadingsForAnUnsupportedPatient()
    {
        HealthAnalyzerUiState healthy = null!, unsupported = null!;
        await Server.WaitPost(() =>
        {
            var patient = SEntMan.GetEntity(_otherPatient);
            var mouse = SSpawnAtPosition("MobMouse", SComp<TransformComponent>(patient).Coordinates);
            var scans = Server.System<ServerHealthAnalyzerSystem>();
            healthy = scans.GetHealthAnalyzerUiState(patient, true);
            unsupported = scans.GetHealthAnalyzerUiState(mouse, true);
        });
        await RunTicksSync(1);
        await RunUntilSynced();
        CryoPodWindow pod = null!;
        await Client.WaitPost(() =>
        {
            pod = new CryoPodWindow();
            pod.OpenCentered();
        });
        try
        {
            var section = pod.FindControl<BoxContainer>("HealthSection");
            var analyzer = pod.FindControl<HealthAnalyzerControl>("HealthAnalyzer");
            var anatomy = analyzer.FindControl<OrbitraAnalyzerAnatomy>("AnalyzerAnatomy");
            var eject = pod.FindControl<Button>("EjectPatientButton");
            await Update(healthy);
            await Client.WaitAssertion(() =>
            {
                Assert.That(anatomy.VisibleInTree, Is.True);
                Assert.That(ZoneDetails(anatomy), Does.Contain(Localized("orbitra-surgery-injury-none")));
                Assert.That(eject.Disabled, Is.False);
            });

            await Update(new HealthAnalyzerUiState { ScanMode = true });
            await Client.WaitAssertion(() =>
            {
                Assert.That(section.Visible, Is.False);
                Assert.That(eject.Disabled, Is.True);
                Assert.That(analyzer.FindControl<RichTextLabel>("PatientName").GetMessage(), Is.Null.Or.Empty);
                Assert.That(ZoneDetails(anatomy), Is.Empty);
            });

            await Update(unsupported);
            await Client.WaitAssertion(() =>
            {
                Assert.That(section.VisibleInTree, Is.True);
                Assert.That(eject.Disabled, Is.False);
                Assert.That(anatomy.VisibleInTree, Is.False);
                Assert.That(analyzer.FindControl<Label>("BloodLabel").Text, Does.Contain("%"));
                Assert.That(analyzer.FindControl<PanelContainer>("AlertsPanel").VisibleInTree, Is.False);
            });
        }
        finally
        {
            await Client.WaitPost(pod.Dispose);
        }

        Task Update(HealthAnalyzerUiState state) => Client.WaitPost(() =>
        {
            pod.Populate(new CryoPodUserMessage(default, state, null, null, null, false));
            DrawFrames();
        });
    }

    [Test]
    public async Task SwitchingBetweenNamesakesClearsReadingsAndExpandedDamage()
    {
        var first = new HealthAnalyzerUiState(_patient, 310f, 0.72f, true, true, false)
        {
            OrbitraTrauma = HumanZones((SurgeryZone.RightArm, SurgeryInjury.Fracture)),
        };
        await Populate(first);
        var toggle = _analyzer.FindControl<Button>("DamageToggle");
        var groups = _analyzer.FindControl<BoxContainer>("GroupsContainer");
        var anatomy = _analyzer.FindControl<OrbitraAnalyzerAnatomy>("AnalyzerAnatomy");
        var temperature = _analyzer.FindControl<Label>("TemperatureLabel");
        await Click(toggle, (Vector2) toggle.PixelSize / 2);
        await Client.WaitAssertion(() =>
        {
            Assert.That(Rich("PatientName"), Does.Contain(PatientName));
            Assert.That(temperature.Text, Does.Contain("°C"));
            Assert.That(ZoneDetails(anatomy), Does.Contain(Localized("orbitra-surgery-injury-fracture")));
            Assert.That(toggle.Pressed && groups.VisibleInTree, Is.True);
            Assert.That(groups.ChildCount, Is.Positive);
        });

        var predicted = new HealthAnalyzerUiState(_patient, float.NaN, 0.72f, true, true, false);
        await Populate(predicted);
        await Client.WaitAssertion(() =>
        {
            Assert.That(temperature.Text, Does.Contain("°C"));
            Assert.That(ZoneDetails(anatomy), Does.Contain(Localized("orbitra-surgery-injury-fracture")));
            Assert.That(toggle.Pressed && groups.VisibleInTree, Is.True);
        });

        await Populate(new HealthAnalyzerUiState(_otherPatient, float.NaN, 1f, true, false, false));
        await Client.WaitAssertion(() =>
        {
            Assert.That(Rich("PatientName"), Does.Contain(PatientName));
            Assert.That(temperature.Text, Is.EqualTo(Localized("health-analyzer-window-entity-unknown-value-text")));
            Assert.That(ZoneDetails(anatomy), Does.Not.Contain(Localized("orbitra-surgery-injury-fracture")));
            Assert.That(_analyzer.FindControl<PanelContainer>("AlertsPanel").Visible, Is.False);
            Assert.That(toggle.Pressed, Is.False);
            Assert.That(groups.VisibleInTree, Is.False);
        });

        await Populate(new HealthAnalyzerUiState(_otherPatient, 309f, 1f, true, false, false) { OrbitraTrauma = HumanZones() });
        await Client.WaitAssertion(() =>
        {
            Assert.That(anatomy.FindControl<Label>("ZoneName").Text, Is.EqualTo(Localized("orbitra-surgery-zone-torso")));
            Assert.That(ZoneDetails(anatomy), Does.Contain(Localized("orbitra-surgery-injury-none")));
        });

        await Populate(new HealthAnalyzerUiState());
        await Client.WaitAssertion(() =>
        {
            Assert.That(_analyzer.FindControl<RichTextLabel>("NoPatientDataText").VisibleInTree, Is.True);
            Assert.That(_analyzer.FindControl<PanelContainer>("Summary").VisibleInTree, Is.False);
            Assert.That(_analyzer.FindControl<ScrollContainer>("DetailsScroll").VisibleInTree, Is.False);
        });
    }

    [Test]
    public async Task OutOfRangeKeepsLastVitalsAndPriorityAlertsUntilScanResumes()
    {
        var live = new HealthAnalyzerUiState(_patient, 314f, 0.65f, true, true, true)
        {
            OrbitraOrgans = new() { [SurgeryOrgan.Heart] = SurgeryOrganCondition.Nonfunctional },
            OrbitraTrauma = HumanZones((SurgeryZone.Torso, SurgeryInjury.InternalBleeding)),
        };
        await Populate(live);
        var temperature = _analyzer.FindControl<Label>("TemperatureLabel");
        var blood = _analyzer.FindControl<Label>("BloodLabel");
        var lastTemperature = temperature.Text;
        var lastBlood = blood.Text;
        var lastAlerts = Rich("AlertsText");
        await Client.WaitAssertion(() =>
        {
            var failure = lastAlerts.IndexOf(Localized("orbitra-analyzer-alert-heart-failed"), StringComparison.Ordinal);
            var bleeding = lastAlerts.IndexOf(Localized("orbitra-analyzer-alert-internal"), StringComparison.Ordinal);
            var warning = lastAlerts.IndexOf(Localized("orbitra-analyzer-alert-resuscitation"), StringComparison.Ordinal);
            Assert.That(failure, Is.GreaterThanOrEqualTo(0));
            Assert.That(bleeding, Is.GreaterThan(failure));
            Assert.That(warning, Is.GreaterThan(bleeding));
            Assert.That(lastBlood, Does.Contain("65"));
            Assert.That(lastTemperature, Does.Contain("°C"));
        });

        var next = new HealthAnalyzerUiState(_patient, 298f, 0.9f, false, false, false) { OrbitraTrauma = HumanZones() };
        await Populate(next);
        await Client.WaitAssertion(() =>
        {
            Assert.That(Rich("ScanModeLabel"), Does.Contain(Localized("orbitra-analyzer-paused")));
            Assert.That(temperature.Text, Is.EqualTo(lastTemperature));
            Assert.That(blood.Text, Is.EqualTo(lastBlood));
            Assert.That(Rich("AlertsText"), Is.EqualTo(lastAlerts));
        });

        next.ScanMode = true;
        await Populate(next);
        await Client.WaitAssertion(() =>
        {
            Assert.That(Rich("ScanModeLabel"), Does.Contain(Localized("orbitra-analyzer-live")));
            Assert.That(temperature.Text, Is.Not.EqualTo(lastTemperature));
            Assert.That(blood.Text, Does.Contain("90"));
            Assert.That(_analyzer.FindControl<PanelContainer>("AlertsPanel").VisibleInTree, Is.False);
        });
    }

    [Test]
    public async Task ScanShowsAnEvacuatedPatientsIncisionAndDressing()
    {
        const SurgeryZone zone = SurgeryZone.LeftArm;
        HealthAnalyzerUiState incised = null!, dressed = null!, closed = null!;
        await Server.WaitPost(() =>
        {
            var patient = SEntMan.GetEntity(_otherPatient);
            var scans = Server.System<ServerHealthAnalyzerSystem>();
            var access = SEntMan.EnsureComponent<OrbitraSurgicalAccessComponent>(patient);
            access.Zones[zone] = SurgeryAccess.Incised;
            incised = scans.GetHealthAnalyzerUiState(patient, true);
            access.DressingRemaining[zone] = 120f;
            dressed = scans.GetHealthAnalyzerUiState(patient, true);
            SEntMan.RemoveComponent<OrbitraSurgicalAccessComponent>(patient);
            closed = scans.GetHealthAnalyzerUiState(patient, true);
        });

        var anatomy = _analyzer.FindControl<OrbitraAnalyzerAnatomy>("AnalyzerAnatomy");
        var map = anatomy.FindControl<OrbitraSurgeryBodyMap>("BodyMap");
        await Populate(incised);
        await Client.WaitAssertion(() =>
        {
            Assert.That(map.Selected, Is.EqualTo(zone));
            Assert.That(map.HasFindings(zone), Is.True);
            Assert.That(ZoneDetails(anatomy), Is.EqualTo(Localized("orbitra-surgery-access-incised")));
        });
        await Populate(dressed);
        await Client.WaitAssertion(() =>
        {
            var reading = Client.ResolveDependency<ILocalizationManager>().GetString("orbitra-surgery-access-dressed",
                ("access", Localized("orbitra-surgery-access-incised")));
            Assert.That(map.DescribeZone!(zone), Does.Contain(reading));
            Assert.That(ZoneDetails(anatomy), Is.EqualTo(reading));
        });
        await Populate(closed);
        await Client.WaitAssertion(() =>
        {
            Assert.That(map.HasFindings(zone), Is.False);
            Assert.That(ZoneDetails(anatomy), Is.EqualTo(Localized("orbitra-surgery-injury-none")));
        });
    }

    [Test]
    public async Task OrganFindingsAppearInTheirOwnRegion()
    {
        var anatomy = _analyzer.FindControl<OrbitraAnalyzerAnatomy>("AnalyzerAnatomy");
        var map = anatomy.FindControl<OrbitraSurgeryBodyMap>("BodyMap");
        foreach (var (patient, selected) in new[] { (_patient, SurgeryZone.Head), (_otherPatient, SurgeryZone.Torso) })
        {
            var organs = OrbitraOrganSystem.Kinds.ToDictionary(organ => organ, organ =>
                OrbitraOrganSystem.ZoneFor(organ) == selected ? SurgeryOrganCondition.Damaged : SurgeryOrganCondition.Healthy);
            await Populate(new HealthAnalyzerUiState(patient, 310f, 1f, true, false, false)
            {
                OrbitraTrauma = HumanZones(),
                OrbitraOrgans = organs,
            });
            await Client.WaitAssertion(() =>
            {
                Assert.That(map.Selected, Is.EqualTo(selected));
                var details = ZoneDetails(anatomy);
                foreach (var zone in Enum.GetValues<SurgeryZone>())
                    Assert.That(map.HasFindings(zone), Is.EqualTo(zone == selected));
                foreach (var organ in OrbitraOrganSystem.Kinds)
                {
                    var name = Localized($"orbitra-surgery-organ-{organ.ToString().ToLowerInvariant()}");
                    Assert.That(details.Contains(name), Is.EqualTo(OrbitraOrganSystem.ZoneFor(organ) == selected));
                }
                var effect = selected == SurgeryZone.Head ? "eyes" : "kidneys";
                Assert.That(details, Does.Contain(Localized($"orbitra-analyzer-anatomy-{effect}-damaged")));
            });
        }
    }

    [Test]
    public async Task SilhouetteShowsPatientRightArmAndShinAndKeepsVitalsVisibleOnResize()
    {
        await Populate(new HealthAnalyzerUiState(_patient, 310f, 1f, true, false, false)
        {
            OrbitraTrauma = HumanZones((SurgeryZone.RightArm, SurgeryInjury.Fracture), (SurgeryZone.RightLeg, SurgeryInjury.Shrapnel)),
        });
        var anatomy = _analyzer.FindControl<OrbitraAnalyzerAnatomy>("AnalyzerAnatomy");
        var map = anatomy.FindControl<OrbitraSurgeryBodyMap>("BodyMap");

        foreach (var (spriteX, spriteY, zone, injury) in new[]
                 {
                     (12.5f, 26.5f, "rightleg", "shrapnel"),
                     (8.5f, 17.5f, "rightarm", "fracture"),
                 })
        {
            var zoom = MathF.Max(1, MathF.Floor(MathF.Min(map.PixelWidth - 16 * map.UIScale, map.PixelHeight - 12 * map.UIScale) / 32));
            var pixel = (Vector2) map.PixelSize / 2 + (new Vector2(spriteX, spriteY) - new Vector2(16)) * zoom;
            await Click(map, pixel);
            await Client.WaitAssertion(() =>
            {
                Assert.That(anatomy.FindControl<Label>("ZoneName").Text, Does.Contain(Localized($"orbitra-surgery-zone-{zone}")));
                Assert.That(ZoneDetails(anatomy), Does.Contain(Localized($"orbitra-surgery-injury-{injury}")));
            });
        }

        var readings = new HealthAnalyzerUiState(_patient, 310f, 1f, true, false, false)
        {
            OrbitraTrauma = HumanZones((SurgeryZone.RightArm, SurgeryInjury.Fracture | SurgeryInjury.Splinted)),
        };
        await Populate(readings);
        await Client.WaitAssertion(() =>
        {
            var details = ZoneDetails(anatomy);
            Assert.That(details, Does.Contain(Localized("orbitra-surgery-injury-splinted")));
            Assert.That(details, Does.Contain("15"));
        });
        readings.OrbitraTrauma = HumanZones();
        await Populate(readings);
        await Client.WaitAssertion(() => Assert.That(ZoneDetails(anatomy),
            Is.EqualTo(Localized("orbitra-surgery-injury-none"))));

        var scroll = _analyzer.FindControl<ScrollContainer>("DetailsScroll");
        var blood = _analyzer.FindControl<Label>("BloodLabel");
        await Client.WaitPost(() =>
        {
            _window.SetSize = new Vector2(330, 355);
            DrawFrames();
            scroll.VScroll = float.MaxValue;
            DrawFrames();
        });
        await Client.WaitAssertion(() =>
        {
            Assert.That(scroll.VScroll, Is.Positive);
            Assert.That(anatomy.FindControl<BoxContainer>("Columns").Orientation, Is.EqualTo(BoxContainer.LayoutOrientation.Vertical));
            Assert.That(blood.GlobalPosition.Y, Is.GreaterThanOrEqualTo(_window.GlobalPosition.Y));
            Assert.That(blood.GlobalPosition.Y + blood.Height, Is.LessThanOrEqualTo(scroll.GlobalPosition.Y));
            Assert.That(scroll.GlobalPosition.Y + scroll.Height, Is.LessThanOrEqualTo(_window.GlobalPosition.Y + _window.Height + 1));
        });
    }

    private static Dictionary<SurgeryZone, SurgeryInjury> HumanZones(params (SurgeryZone Zone, SurgeryInjury Injury)[] injuries)
    {
        var zones = Enum.GetValues<SurgeryZone>().ToDictionary(zone => zone, _ => SurgeryInjury.None);
        foreach (var (zone, injury) in injuries)
            zones[zone] = injury;
        return zones;
    }

    private Task Populate(HealthAnalyzerUiState state) => Client.WaitPost(() =>
    {
        _window.Populate(state);
        DrawFrames();
    });

    private void DrawFrames()
    {
        var ui = Client.ResolveDependency<IUserInterfaceManager>();
        for (var frame = 0; frame < 12; frame++)
            OrbitraWindowLifecycleTest.Frame(ui, 0.025f);
    }

    private string Localized(string key) => Client.ResolveDependency<ILocalizationManager>().GetString(key);

    private string Rich(string name) => _analyzer.FindControl<RichTextLabel>(name).GetFormattedMessage()?.ToString() ?? string.Empty;

    private static string ZoneDetails(OrbitraAnalyzerAnatomy anatomy) =>
        anatomy.FindControl<RichTextLabel>("ZoneDetails").GetFormattedMessage()?.ToString() ?? string.Empty;

    private async Task Click(Control control, Vector2 pixel)
    {
        var screen = new ScreenCoordinates(control.GlobalPixelPosition + pixel, control.Window!.Id);
        foreach (var phase in new[] { BoundKeyState.Down, BoundKeyState.Up })
            await Client.DoGuiEvent(control, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, phase,
                screen, false, pixel / control.UIScale, pixel));
    }
}
