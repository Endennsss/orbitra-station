using Content.Client.Launcher;
using Robust.Client.State;

namespace Content.Client.MainMenu;

public sealed partial class MainScreen
{
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private ExtendedDisconnectInformationManager _disconnectInfo = default!;

    private void RestoreDirectAddress()
    {
        if (_disconnectInfo.DirectTarget is not { } target)
            return;

        _mainMenuControl.AddressBox.Text = target.Address;
        _disconnectInfo.DirectTarget = null;
    }
}
