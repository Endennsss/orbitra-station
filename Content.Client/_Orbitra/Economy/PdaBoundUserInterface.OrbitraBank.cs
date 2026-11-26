using Content.Client._Orbitra.Economy;
using Content.Shared._Orbitra.Economy;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Client.PDA;

public sealed partial class PdaBoundUserInterface
{
    internal PdaMenu? Menu => _menu;

    private void RequestBankSnapshot(Control fragment)
    {
        if (fragment is OrbitraBankPdaFragment bank)
            bank.RequestAccount(() => SendMessage(new CartridgeUiMessage(new OrbitraBankPdaRefreshMessage())));
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is OrbitraBankPdaSnapshotMessage snapshot)
            UpdateState(snapshot.State);
    }
}
