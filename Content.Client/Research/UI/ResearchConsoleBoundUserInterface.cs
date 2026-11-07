using Content.Shared.Research.Components;
// Orbitra-Edit - прототипы обрабатывает окно дерева.
using Content.Client._Orbitra.Research.UI; // Orbitra-Edit - окно дерева исследований.
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Research.UI;

[UsedImplicitly]
public sealed class ResearchConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private OrbitraResearchConsoleMenu? _consoleMenu; // Orbitra-Edit - окно дерева исследований.

    public ResearchConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        var owner = Owner;

        _consoleMenu = this.CreateWindow<OrbitraResearchConsoleMenu>(); // Orbitra-Edit - окно дерева исследований.
        _consoleMenu.SetEntity(owner);

        _consoleMenu.OnTechnologyCardPressed += id =>
        {
            SendMessage(new ConsoleUnlockTechnologyMessage(id));
        };

        _consoleMenu.OnServerButtonPressed += () =>
        {
            SendMessage(new ConsoleServerSelectionMessage());
        };
    }

    public override void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        base.OnProtoReload(args);

        // Orbitra edit start - окно само обновляет кеш затронутых технологий и рецептов.
        _consoleMenu?.OnProtoReload(args);
        // Orbitra edit end
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ResearchConsoleBoundInterfaceState castState)
            return;
        _consoleMenu?.UpdatePanels(castState);
        _consoleMenu?.UpdateInformationPanel(castState);
    }
}
