using Content.Client._Orbitra.Stylesheets;
using Content.Client.Stylesheets.Palette;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Atmos;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.MedicalScanner;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.HealthAnalyzer.UI;

public sealed partial class HealthAnalyzerControl
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private NetEntity? _patient;
    private string? _displayName;
    private bool? _live;
    private bool _internalBleeding;
    private SurgeryOrganCondition _heart;
    private SurgeryOrganCondition _lungs;
    private (bool, bool, bool, SurgeryOrganCondition, SurgeryOrganCondition) _lastAlerts;

    /// <summary>Общие показатели приходят и из предсказания, а анатомия и температура уточняются сервером.</summary>
    public void Populate(HealthAnalyzerUiState state)
    {
        if (_patient != state.TargetEntity)
        {
            ClearPatient();
            _patient = state.TargetEntity;
        }

        if (_patient == null)
            return;

        // За пределами дальности сохраняем именно последний доступный замер, даже если цель ещё видима в мире.
        if (!state.ScanMode || !_entities.TryGetEntity(_patient, out var patient) || _entities.Deleted(patient))
        {
            SetConnection(false);
            if (!Summary.Visible)
                NoPatientDataText.SetMessage(Loc.GetString("orbitra-analyzer-paused"));
            return;
        }

        NoPatientDataText.Visible = false;
        Summary.Visible = DetailsScroll.Visible = true;
        SetConnection(true);

        var uid = patient.Value;
        var name = Identity.Name(uid, _entities);
        if (name != _displayName)
        {
            _displayName = name;
            PatientName.SetMessage(name);
        }

        var species = _entities.TryGetComponent<HumanoidProfileComponent>(uid, out var humanoid)
            ? Loc.GetString(_prototypes.Index(humanoid.Species).Name)
            : Loc.GetString("health-analyzer-window-entity-unknown-species-text");
        SetReading(SpeciesLabel, species, OrbitraPalettes.IconNormal);
        SpriteView.SetEntity(uid);
        SpriteView.Visible = true;

        var status = _entities.TryGetComponent<MobStateComponent>(uid, out var mob) ? mob.CurrentState : (MobState?) null;
        var (statusKey, color) = status switch
        {
            MobState.Alive => ("health-analyzer-window-entity-alive-text", Palettes.Green.Text),
            MobState.Critical => ("health-analyzer-window-entity-critical-text", Palettes.Amber.Text),
            MobState.Dead => ("health-analyzer-window-entity-dead-text", Palettes.Red.Text),
            _ => ("health-analyzer-window-entity-unknown-text", OrbitraPalettes.IconNormal),
        };
        SetReading(StatusLabel, Loc.GetString(statusKey), color);

        var unknown = Loc.GetString("health-analyzer-window-entity-unknown-value-text");
        SetValue(BloodLabel, float.IsFinite(state.BloodLevel) ? $"{state.BloodLevel * 100:F1} %" : unknown);
        if (float.IsFinite(state.Temperature))
        {
            SetValue(TemperatureLabel, $"{state.Temperature - Atmospherics.T0C:F1} °C");
            TemperatureLabel.ToolTip = $"{state.Temperature:F1} K";
        }
        else if (state.OrbitraTrauma != null)
        {
            SetValue(TemperatureLabel, unknown);
            TemperatureLabel.ToolTip = null;
        }

        if (state.OrbitraTrauma is { } zones)
        {
            AnalyzerAnatomy.SetReadings(zones, state.OrbitraOrgans, uid);
            _internalBleeding = (zones.GetValueOrDefault(SurgeryZone.Torso) & SurgeryInjury.InternalBleeding) != 0;
            _heart = state.OrbitraOrgans?.GetValueOrDefault(SurgeryOrgan.Heart) ?? SurgeryOrganCondition.Healthy;
            _lungs = state.OrbitraOrgans?.GetValueOrDefault(SurgeryOrgan.Lungs) ?? SurgeryOrganCondition.Healthy;
        }
        UpdateAlerts(state.Bleeding, state.Unrevivable);
        UpdateDamage(uid);
    }

    private void ClearPatient()
    {
        Summary.Visible = DetailsScroll.Visible = AlertsPanel.Visible = false;
        NoPatientDataText.Visible = true;
        NoPatientDataText.SetMessage(Loc.GetString("health-analyzer-window-no-patient-data-text"));
        _displayName = null;
        _live = null;
        SpriteView.SetEntity((EntityUid?) null);
        PatientName.Clear();
        SpeciesLabel.Clear();
        StatusLabel.Clear();
        ScanModeLabel.Clear();
        BloodLabel.Text = DamageLabel.Text = null;
        TemperatureLabel.Text = Loc.GetString("health-analyzer-window-entity-unknown-value-text");
        TemperatureLabel.ToolTip = null;
        _heart = _lungs = SurgeryOrganCondition.Healthy;
        _internalBleeding = false;
        _lastAlerts = default;
        AlertsText.Clear();
        AnalyzerAnatomy.Clear();
        ResetDamage();
        DetailsScroll.VScrollTarget = DetailsScroll.VScroll = 0;
    }

    private void SetConnection(bool live)
    {
        if (_live == live)
            return;
        _live = live;
        ScanModeLabel.SetMessage(Loc.GetString(live ? "orbitra-analyzer-live" : "orbitra-analyzer-paused"),
            live ? OrbitraPalettes.IconNormal : Palettes.Amber.Text);
        SpriteView.Visible = live;
    }

    private static void SetValue(Label label, string value)
    {
        if (label.Text != value)
            label.Text = value;
    }

    private static void SetReading(RichTextLabel label, string value, Color color)
    {
        if (label.Text != value)
            label.SetMessage(value, color);
    }

    private void UpdateAlerts(bool bleeding, bool unrevivable)
    {
        var alerts = (bleeding, _internalBleeding, unrevivable, _heart, _lungs);
        if (_lastAlerts == alerts)
            return;
        _lastAlerts = alerts;
        var messages = new List<string>(5);
        AddOrganAlert(_heart, "orbitra-analyzer-alert-heart-failed", "orbitra-analyzer-alert-heart-missing");
        AddOrganAlert(_lungs, "orbitra-analyzer-alert-lungs-failed", "orbitra-analyzer-alert-lungs-missing");
        if (_internalBleeding)
            messages.Add(Loc.GetString("orbitra-analyzer-alert-internal"));
        if (bleeding)
            messages.Add(Loc.GetString("orbitra-analyzer-alert-external"));
        if (unrevivable)
            messages.Add(Loc.GetString("orbitra-analyzer-alert-resuscitation"));

        AlertsPanel.Visible = messages.Count != 0;
        AlertsText.SetMessage(string.Join("\n", messages), Palettes.Amber.Text);

        void AddOrganAlert(SurgeryOrganCondition condition, string failed, string missing)
        {
            if (condition is SurgeryOrganCondition.Nonfunctional or SurgeryOrganCondition.Missing)
                messages.Add(Loc.GetString(condition == SurgeryOrganCondition.Missing ? missing : failed));
        }
    }
}
