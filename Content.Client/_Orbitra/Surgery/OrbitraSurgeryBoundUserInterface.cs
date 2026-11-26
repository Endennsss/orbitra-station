using Content.Shared._Orbitra.Surgery;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.Surgery;

[UsedImplicitly]
public sealed class OrbitraSurgeryBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private OrbitraSurgeryWindow? _window;
    private NetEntity? _patient;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<OrbitraSurgeryWindow>();
        _window.StepRequested += SendMessage;
        _window.CancelRequested += SendMessage;
        Update<OrbitraSurgeryState>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not OrbitraSurgeryState surgery || _window == null)
            return;
        if (_patient != null && _patient != surgery.Patient)
        {
            Close();
            return;
        }
        _patient = surgery.Patient;
        _window.UpdateState(surgery);
    }
}
