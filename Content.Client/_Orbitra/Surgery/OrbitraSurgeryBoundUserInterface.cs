using Content.Shared._Orbitra.Surgery;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.Surgery;

[UsedImplicitly]
public sealed class OrbitraSurgeryBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    internal OrbitraSurgeryWindow? Window { get; private set; }
    private NetEntity? _patient;

    protected override void Open()
    {
        base.Open();
        // При синхронизации клиент может повторно получить открытие того же стола.
        if (Window != null)
            return;

        Window = this.CreateWindow<OrbitraSurgeryWindow>();
        Window.MonitorTable = EntMan.GetNetEntity(Owner);
        Window.StepRequested += SendMessage;
        Window.CancelRequested += SendMessage;
        Update<OrbitraSurgeryState>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not OrbitraSurgeryState surgery || Window == null)
            return;
        if (_patient != null && _patient != surgery.Patient)
        {
            Close();
            return;
        }
        _patient = surgery.Patient;
        Window.UpdateState(surgery);
    }
}
