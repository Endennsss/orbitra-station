using Content.Shared._Orbitra.Ratvar;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.Ratvar;

[UsedImplicitly]
public sealed class OrbitraRatvarCrystalBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private OrbitraRatvarCrystalWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<OrbitraRatvarCrystalWindow>();
        _window.Project += target => SendMessage(new OrbitraRatvarCrystalProjectMessage(target));
        _window.Rename += name => SendMessage(new OrbitraRatvarCrystalRenameMessage(name));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is OrbitraRatvarCrystalUiState crystal) _window?.Update(crystal);
    }
}
