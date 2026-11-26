using Content.Client._Orbitra.UserInterface;
using Content.Shared._Orbitra.Surgery;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.Surgery;

public sealed partial class OrbitraSurgeryWindow
{
    // Инструмент берём из локальной активной руки, ход операции — только из состояния стола.
    private int Step => _state?.ActiveZone == _zone && _state?.ActiveProcedure == _procedure ? _state?.Step ?? 0 : 0;
    private string? _requirement;
    private bool _toolReady;

    private void UpdateStep()
    {
        _requirement = null;
        StepPanel.Visible = false;
        if (_procedure is { } procedure && _zone is { } zone)
        {
            var steps = OrbitraSurgerySteps.Get(procedure);
            var step = Step;
            if ((uint) step < (uint) steps.Length)
            {
                StepPanel.Visible = true;
                _requirement = steps[step];
                StepNumber.Text = _loc.GetString("orbitra-surgery-step-count", ("step", step + 1), ("count", steps.Length));
                StepName.SetMessage(_loc.GetString($"orbitra-surgery-step-{procedure.ToString().ToLowerInvariant()}-{step + 1}"));
                if (_requirement == "DetachedBody")
                {
                    RequiredTool.SetMessage(_loc.GetString("orbitra-surgery-original-limb", ("zone", ZoneNameFor(zone))));
                    RequiredIcon.Texture = _sprites.GetPrototypeIcon(zone switch
                    {
                        SurgeryZone.RightArm => "OrganHumanArmRight",
                        SurgeryZone.LeftArm => "OrganHumanArmLeft",
                        SurgeryZone.RightLeg => "OrganHumanLegRight",
                        _ => "OrganHumanLegLeft",
                    }).Default;
                }
                else
                {
                    RequiredTool.SetMessage(_prototype.Index<EntityPrototype>(_requirement).Name);
                    RequiredIcon.Texture = _sprites.GetPrototypeIcon(_requirement).Default;
                }
            }
        }
        UpdateHand();
        UpdateProgress();
    }

    private bool HasRequiredItem()
    {
        if (_requirement == null || _held is not { } held)
            return false;
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
        HeldTool.SetMessage(name);
        UpdateActions();
    }

    private void UpdateActions()
    {
        _toolReady = HasRequiredItem();
        ActionButton.Disabled = _state?.Patient == null || _state.Busy || !_toolReady;
        var action = _confirmAmputation ? "orbitra-surgery-confirm-action" :
            _state?.Busy == true ? "orbitra-surgery-in-progress" : "orbitra-surgery-start-step";
        ActionButton.Text = _loc.GetString(action);
        if (_confirmAmputation)
            ActionButton.AddStyleClass(OrbitraButtonStyles.Danger);
        else
            ActionButton.RemoveStyleClass(OrbitraButtonStyles.Danger);
        CancelButton.Visible = _confirmAmputation || _state is { Busy: false, ActiveProcedure: not null };
        var hint = _state?.Patient == null ? "orbitra-surgery-table-empty" :
            _state.Injuries.Count == 0 ? "orbitra-surgery-unsupported" :
            _state.Busy ? "orbitra-surgery-busy" :
            _confirmAmputation ? "orbitra-surgery-confirm-warning" :
            _completed ? "orbitra-surgery-complete" :
            _zone == null ? "orbitra-surgery-select-zone" :
            _state.ActiveZone != null && _zone != _state.ActiveZone ? "orbitra-surgery-other-zone" :
            _procedure == null ? (ProcedurePanel.Visible ? "orbitra-surgery-select-procedure" : "orbitra-surgery-no-procedure") :
            _toolReady ? "orbitra-surgery-ready" :
            _requirement == "DetachedBody" && _held != null ? "orbitra-surgery-wrong-limb" : "orbitra-surgery-take-tool";
        var message = _loc.GetString(hint, ("patient", _state?.PatientName ?? ""),
            ("zone", (_state?.ActiveZone ?? _zone) is { } region ? ZoneNameFor(region) : ""));
        if (_interrupted)
            message = _loc.GetString("orbitra-surgery-interrupted", ("hint", message));
        Status.ToolTip = message;
        Status.SetMessage(message, _confirmAmputation || _interrupted ? Color.LightSalmon :
            _toolReady && _state?.Busy == false ? Color.PaleTurquoise : Color.White);
    }

    private void RequestStep()
    {
        if (_state is not { Patient: { } patient, Busy: false } || _zone is not { } zone || _procedure is not { } procedure)
            return;
        UpdateHand();
        if (!HasRequiredItem() || !Available(procedure))
            return;
        var step = Step;
        if (procedure == SurgeryProcedure.Amputate && step == 0 && !_confirmAmputation)
        {
            _confirmAmputation = true;
            UpdateActions();
            return;
        }
        var confirmed = _confirmAmputation;
        ClearFeedback();
        UpdateActions();
        StepRequested?.Invoke(new OrbitraSurgeryStepMessage(patient, zone, procedure, step, confirmed));
    }

    private void Cancel()
    {
        if (_confirmAmputation)
        {
            _confirmAmputation = false;
            UpdateActions();
        }
        else if (_state is { Patient: { } patient, ActiveZone: { } zone, ActiveProcedure: { } procedure, Busy: false })
        {
            CancelRequested?.Invoke(new OrbitraSurgeryCancelMessage(patient, zone, procedure, _state.Step));
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (UserInterfaceManager.WindowRoot.Size != _viewport)
            FitToViewport();
        if (_hands.GetActiveHandEntity() != _held)
        {
            _confirmAmputation = false;
            _completed = false;
            UpdateHand();
        }
        else if (_toolReady != HasRequiredItem())
            UpdateActions();
        if (_state?.Busy == true)
            UpdateProgress();
    }

    private void UpdateProgress()
    {
        if (_state?.ActiveProcedure is not { } active)
        {
            Progress.Value = _completed ? 1 : 0;
            return;
        }
        var partial = 0d;
        if (_state is { Busy: true, StartedAt: { } start } && _state.Duration > TimeSpan.Zero)
            partial = Math.Clamp((_timing.CurTime - start) / _state.Duration, 0, 0.99);
        Progress.Value = (float) ((_state.Step + partial) / OrbitraSurgerySteps.Get(active).Length);
    }
}
