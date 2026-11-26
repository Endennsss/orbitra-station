using Content.Client.UserInterface.Fragments;
using Content.Shared._Orbitra.Economy;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.Economy;

public sealed partial class OrbitraBankPdaUi : UIFragment
{
    private BoundUserInterface? _ui;
    private OrbitraBankPdaFragment? _fragment;

    public override Control GetUIFragmentRoot() => _fragment!;

    public override void Setup(BoundUserInterface ui, EntityUid? fragmentOwner)
    {
        if (_ui == ui && _fragment?.Parent != null)
            return;

        _ui = ui;
        _fragment = new OrbitraBankPdaFragment();
        _fragment.TransferRequested += (recipient, amount) =>
            ui.SendMessage(new CartridgeUiMessage(new OrbitraBankPdaTransferMessage(recipient, amount)));
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is OrbitraBankPdaState account)
            _fragment?.UpdateAccount(account);
    }
}
