using Content.Shared._Orbitra.Ratvar;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.Ratvar;

[UsedImplicitly]
public sealed class OrbitraRatvarEminenceBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private OrbitraRatvarEminenceWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<OrbitraRatvarEminenceWindow>();
        _window.SelectMember += mind => SendMessage(new OrbitraRatvarEminenceSelectMessage(mind));
        _window.ClearSelection += () => SendMessage(new OrbitraRatvarEminenceClearMessage());
        _window.Recall += destination => SendMessage(new OrbitraRatvarEminenceRecallMessage(destination));
        _window.MassRecall += destination => SendMessage(new OrbitraRatvarEminenceMassRecallMessage(destination));
        _window.Manipulate += effect => SendMessage(new OrbitraRatvarEminenceRealityMessage(effect));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is OrbitraRatvarEminenceUiState roster) _window?.Update(roster);
    }
}
