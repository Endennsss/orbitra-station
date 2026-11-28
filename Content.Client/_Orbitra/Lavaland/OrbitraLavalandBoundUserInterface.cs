using Content.Shared._Orbitra.Lavaland;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.Lavaland;

[UsedImplicitly]
public sealed class OrbitraLavalandBoundUserInterface(EntityUid owner, Enum key) : BoundUserInterface(owner, key)
{
    private OrbitraLavalandWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<OrbitraLavalandWindow>();
        _window.RequestButton.OnPressed += _ => SendMessage(new OrbitraLavalandRequestMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is LavalandConsoleState ferry && EntMan.TryGetComponent<OrbitraLavalandTerminalComponent>(Owner, out var terminal))
            _window?.Display(ferry, terminal.Side);
    }
}
