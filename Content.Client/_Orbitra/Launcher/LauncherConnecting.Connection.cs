using Robust.Client.State;

namespace Content.Client.Launcher;

public sealed partial class LauncherConnecting
{
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private ExtendedDisconnectInformationManager _disconnectInfo = default!;

    internal bool IsDirectConnection => _disconnectInfo.DirectTarget != null;
    internal bool CanReturnToMenu => IsDirectConnection && CurrentPage != Page.Connecting;
    internal bool WasDocked { get; private set; }

    internal void SetDisconnected(bool wasDocked)
    {
        WasDocked |= wasDocked;
        SetDisconnected();
    }
}
