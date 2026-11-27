using System;
using Robust.Client;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;

namespace Content.Client.Launcher;

/// <summary>
/// So apparently the way that disconnect information is shipped around is really indirect.
/// But honestly, given that content might have additional flags (i.e. hide disconnect button for bans)?
/// This is responsible for collecting any extended disconnect information.
/// </summary>
public sealed partial class ExtendedDisconnectInformationManager
{
    [Dependency] private IClientNetManager _clientNetManager = default!;

    private NetDisconnectedArgs? _lastNetDisconnectedArgs = null;

    // Orbitra added start - адрес нужен и после перехода из меню в игру.
    internal (string Address, string Host, ushort Port)? DirectTarget { get; set; }
    // Orbitra added end

    public NetDisconnectedArgs? LastNetDisconnectedArgs
    {
        get => _lastNetDisconnectedArgs;
        private set
        {
            _lastNetDisconnectedArgs = value;
            LastNetDisconnectedArgsChanged?.Invoke(value);
        }
    }

    // BE CAREFUL!
    // This may fire at an arbitrary time before or after whatever code that needs it.
    public event Action<NetDisconnectedArgs?>? LastNetDisconnectedArgsChanged;

    public void Initialize()
    {
        _clientNetManager.Disconnect += OnNetDisconnect;
        // Orbitra added start - новая попытка сбрасывает причину и задержку предыдущего отключения.
        _clientNetManager.ClientConnectStateChanged += state =>
        {
            if (state == ClientConnectionState.ResolvingHost)
                LastNetDisconnectedArgs = null;
        };
        // Orbitra added end
    }

    private void OnNetDisconnect(object? sender, NetDisconnectedArgs args)
    {
        LastNetDisconnectedArgs = args;
    }
}

