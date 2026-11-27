using Content.Shared._Orbitra.Surgery;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.Surgery;

public sealed partial class OrbitraSurgeryWindow
{
    private string? _requirement;
    private bool _toolReady;
    private int _activeStepCount;
    private bool ViewingActive => _state is { ActiveProcedure: { } active, ActiveZone: { } zone } &&
        zone == _zone && (_procedure == null || _procedure == active) && _organ == _state.ActiveOrgan;

    private string TaskName(SurgeryProcedure procedure, SurgeryOrgan? organ) =>
        OrbitraOrganSystem.IsProcedure(procedure) && organ is { } kind
            ? _loc.GetString("orbitra-surgery-task-organ", ("task", ProcedureName(procedure)), ("organ", OrganName(kind)))
            : ProcedureName(procedure);

    private void UpdateStep()
    {
        _requirement = null;
        StepPanel.Visible = false;
        SelectionSummary.Visible = _zone != null && _state?.Patient != null;
        if (_state != null && _zone is { } zone)
        {
            var site = _loc.GetString("orbitra-surgery-zone-status", ("zone", ZoneNameFor(zone)), ("status", AccessAt(zone)));
            var displayed = _procedure ?? (_state.ActiveZone == zone ? _state.ActiveProcedure : null);
            if (displayed is { } procedure)
            {
                var organKind = _procedure != null ? _organ : _state.ActiveOrgan;
                SelectionSummary.SetMessage(_loc.GetString("orbitra-surgery-task-site", ("site", site), ("task", TaskName(procedure, organKind))));
                var steps = OrbitraSurgerySteps.Get(_prototype, procedure, zone);
                // Соседний орган может требовать той же операции, но имеет своё начало.
                var step = _state.ActiveProcedure == procedure && _state.ActiveZone == zone && organKind != _state.ActiveOrgan
                    ? 0 : _state.GetStep(procedure, zone);
                if ((uint) step < (uint) steps.Length)
                {
                    _requirement = steps[step];
                    StepNumber.Text = _loc.GetString("orbitra-surgery-step-count", ("step", step + 1), ("count", steps.Length));
                    var stepKey = procedure == SurgeryProcedure.OpenAccess
                        ? $"orbitra-surgery-step-openaccess-{_requirement.ToLowerInvariant()}"
                        : $"orbitra-surgery-step-{procedure.ToString().ToLowerInvariant()}-{step + 1}";
                    StepName.Visible = false;
                    if (!_collapsed && _loc.TryGetString(stepKey, out var action))
                    {
                        StepName.SetMessage(action);
                        StepName.Visible = true;
                    }
                    var (item, name) = RequiredItem(_requirement, zone, organKind);
                    RequiredIcon.Texture = _sprites.GetPrototypeIcon(item).Default;
                    RequiredTool.SetMessage(_loc.GetString("orbitra-surgery-next-tool", ("tool", name)));
                    StepPanel.Visible = true;
                }
            }
            else
            {
                SelectionSummary.Visible = _collapsed;
                SelectionSummary.SetMessage(site);
            }
        }

        _activeStepCount = _state is { ActiveProcedure: { } active, ActiveZone: { } activeZone }
            ? OrbitraSurgerySteps.Get(_prototype, active, activeZone).Length : 0;
        SetColumns(Width);
        UpdateHand();
        UpdateProgress();
    }

    private (string Prototype, string Name) RequiredItem(string requirement, SurgeryZone zone, SurgeryOrgan? organ)
    {
        if (requirement == OrbitraSurgerySteps.Prosthesis)
        {
            var arm = zone is SurgeryZone.LeftArm or SurgeryZone.RightArm;
            return (arm ? "OrbitraProstheticArm" : "OrbitraProstheticLeg",
                _loc.GetString(arm ? "orbitra-surgery-require-arm" : "orbitra-surgery-require-leg"));
        }
        if (requirement == OrbitraSurgerySteps.DonorOrgan && organ is { } kind)
            return (OrbitraOrganSystem.Prototype(kind), _loc.GetString("orbitra-surgery-organ-required", ("organ", OrganName(kind))));
        if (requirement == "DetachedBody")
        {
            var prototype = zone switch
            {
                SurgeryZone.RightArm => "OrganHumanArmRight",
                SurgeryZone.LeftArm => "OrganHumanArmLeft",
                SurgeryZone.RightLeg => "OrganHumanLegRight",
                _ => "OrganHumanLegLeft",
            };
            return (prototype, _loc.GetString("orbitra-surgery-original-limb", ("zone", ZoneNameFor(zone))));
        }
        return (requirement, _prototype.Index<EntityPrototype>(requirement).Name);
    }

    private bool HasRequiredItem()
    {
        if (_requirement == null || _held is not { } held)
            return false;
        if (_requirement == OrbitraSurgerySteps.Prosthesis)
            return _zone is { } zone && _entity.TryGetComponent<OrbitraProsthesisComponent>(held, out var kit) && kit.Fits(zone);
        if (_requirement == OrbitraSurgerySteps.DonorOrgan)
            return _organ is { } kind && _organs.IsDonor(held, kind);
        if (_requirement == "Gauze")
            return _entity.TryGetComponent<OrbitraTraumaDressingComponent>(held, out var dressing) && dressing.Treatment == SurgeryInjury.Bandaged;
        if (_requirement != "DetachedBody")
            return _entity.TryGetComponent<OrbitraSurgicalToolComponent>(held, out var tool) && tool.Kind == _requirement;
        return _entity.TryGetComponent<OrbitraDetachedLimbComponent>(held, out var limb) && limb.Zone == _zone &&
            _entity.TryGetNetEntity(limb.OriginalOwner, out var owner) && owner == _state?.Patient;
    }

    private void UpdateHand()
    {
        _held = _hands.GetActiveHandEntity();
        HandIcon.SetEntity(_held);
        var name = _entity.TryGetComponent<MetaDataComponent>(_held, out var meta)
            ? meta.EntityName : _loc.GetString("orbitra-surgery-empty-hand");
        if (_held is { } held && _entity.TryGetComponent<OrbitraOrganComponent>(held, out var organ) && _organs.IsHumanOrgan(held, organ.Kind))
            name = OrganReading(organ.Kind, organ.Condition);
        HeldTool.SetMessage(name);
        UpdateActions();
    }

    private bool IsArmed() =>
        _procedure != null && _entity.TryGetComponent<OrbitraSurgerySelectionComponent>(_players.LocalEntity, out var choice) &&
        _entity.TryGetNetEntity(choice.Patient, out var patient) && patient == _state?.Patient &&
        _entity.TryGetNetEntity(choice.Table, out var table) && table == MonitorTable &&
        choice.Zone == _zone && choice.Procedure == _procedure && choice.Organ == _organ;

    private void UpdateActions()
    {
        _toolReady = HasRequiredItem();
        _armed = IsArmed();
        ConfirmButton.Visible = _awaitingConfirmation;
        if (_awaitingConfirmation)
            ConfirmButton.Text = _loc.GetString(_procedure == SurgeryProcedure.ExtractOrgan
                ? "orbitra-surgery-organ-confirm-action" : "orbitra-surgery-confirm-action");
        CancelButton.Visible = _awaitingConfirmation || _state?.Busy == false && ViewingActive;
        CancelButton.Text = _loc.GetString(_awaitingConfirmation ? "orbitra-surgery-cancel" : "orbitra-surgery-abandon");
        ActionButtons.Visible = ConfirmButton.Visible || CancelButton.Visible;
        var hint = _state?.Patient == null ? "orbitra-surgery-table-empty" :
            _state.Injuries.Count == 0 ? "orbitra-surgery-unsupported" :
            _state.Busy ? "orbitra-surgery-busy" :
            _awaitingConfirmation && _procedure == SurgeryProcedure.ExtractOrgan ? "orbitra-surgery-organ-confirm" :
            _awaitingConfirmation ? "orbitra-surgery-confirm-warning" :
            _feedback != null ? _feedback :
            _zone == null ? "orbitra-surgery-select-zone" :
            _state.ActiveProcedure != null && !ViewingActive ? "orbitra-surgery-other-zone" :
            _procedure == null ? (ProcedurePanel.Visible ? "orbitra-surgery-select-procedure" : "orbitra-surgery-no-procedure") :
            !_armed ? "orbitra-surgery-select-procedure" :
            _toolReady ? "orbitra-surgery-ready" :
            _requirement == OrbitraSurgerySteps.DonorOrgan && _held != null ? "orbitra-surgery-organ-incompatible" :
            _requirement == "DetachedBody" && _held != null ? "orbitra-surgery-wrong-limb" : "orbitra-surgery-take-tool";
        var message = _loc.GetString(hint, ("patient", _state?.PatientName ?? ""), ("organ", _organ is { } kind ? OrganName(kind) : ""),
            ("task", _state?.ActiveProcedure is { } active ? TaskName(active, _state.ActiveOrgan) : ""),
            ("zone", (_state?.ActiveZone ?? _zone) is { } region ? ZoneNameFor(region) : ""));
        Status.ToolTip = message;
        Status.SetMessage(message, _awaitingConfirmation ? OrbitraSurgerySheetlet.Finding : _toolReady && _armed ? OrbitraSurgerySheetlet.Accent : Color.White);
    }

    private void ArmTask()
    {
        if (_state is not { Patient: { } patient, Busy: false } || _zone is not { } zone ||
            _procedure is not { } procedure || !Available(procedure))
            return;
        TaskSelected?.Invoke(new OrbitraSurgeryStepMessage(patient, zone, procedure, _state.GetStep(procedure, zone),
            _awaitingConfirmation, OrbitraOrganSystem.IsProcedure(procedure) ? _organ : null));
        _awaitingConfirmation = false;
        UpdateActions();
    }

    private void Cancel()
    {
        if (_awaitingConfirmation)
        {
            ClearTask();
            RefreshSelection();
        }
        else if (ViewingActive && _state is { Patient: { } patient, ActiveZone: { } zone, ActiveProcedure: { } procedure, Busy: false })
        {
            CancelRequested?.Invoke(new OrbitraSurgeryCancelMessage(patient, zone, procedure, _state.Step, _state.ActiveOrgan));
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateMonitor();
        if (UserInterfaceManager.WindowRoot.Size != _viewport)
            FitToViewport();
        if (_hands.GetActiveHandEntity() != _held)
            UpdateHand();
        else if (_toolReady != HasRequiredItem() || _armed != IsArmed())
            UpdateActions();
        if (_state?.Busy == true)
            UpdateProgress();
    }

    private void UpdateProgress()
    {
        Progress.Visible = _activeStepCount > 0;
        if (_state == null || _activeStepCount == 0)
            return;
        var partial = 0d;
        if (_state is { Busy: true, StartedAt: { } start } && _state.Duration > TimeSpan.Zero)
            partial = Math.Clamp((_timing.CurTime - start) / _state.Duration, 0, 0.99);
        Progress.Value = (float) ((_state.Step + partial) / _activeStepCount);
    }
}
