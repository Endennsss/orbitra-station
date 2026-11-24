using Content.Shared._Orbitra.Economy;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.Economy;

[UsedImplicitly]
public sealed class OrbitraAtmBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    internal OrbitraAtmWindow? Window { get; private set; }

    protected override void Open()
    {
        base.Open();
        Window = this.CreateWindow<OrbitraAtmWindow>();
        Window.InsertCardRequested += () => SendMessage(new OrbitraAtmInsertCardMessage());
        Window.EjectCardRequested += () => SendMessage(new OrbitraAtmEjectCardMessage());
        Window.DepositRequested += () => SendMessage(new OrbitraAtmDepositMessage());
        Window.WithdrawRequested += amount => SendMessage(new OrbitraAtmWithdrawMessage(amount));
        Window.TransferRequested += (recipient, amount) => SendMessage(new OrbitraAtmTransferMessage(recipient, amount));
        SendMessage(new OrbitraAtmRefreshMessage());
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is OrbitraAtmSnapshotMessage snapshot)
            Window?.UpdateAccount(snapshot);
    }
}
