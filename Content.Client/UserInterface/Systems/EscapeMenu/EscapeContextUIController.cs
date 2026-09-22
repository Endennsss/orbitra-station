using Content.Client.UserInterface.Systems.Info;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client.UserInterface.Systems.EscapeMenu;

[UsedImplicitly]
public sealed partial class EscapeContextUIController : UIController
{
    [Dependency] private IInputManager _inputManager = default!;

    [Dependency] private CloseRecentWindowUIController _closeRecentWindowUIController = default!;
    [Dependency] private EscapeUIController _escapeUIController = default!;

    public override void Initialize()
    {
        _inputManager.SetInputCommand(ContentKeyFunctions.EscapeContext,
            InputCmdHandler.FromDelegate(_ => CloseWindowOrOpenGameMenu()));
    }

    private void CloseWindowOrOpenGameMenu()
    {
        if (_closeRecentWindowUIController.HasClosableWindow())
        {
            _closeRecentWindowUIController.CloseMostRecentWindow();
        }
        else
        {
            // Orbitra added start - выход из редактора проходит через проверку сохранения.
            if (UIManager.ActiveScreen is Content.Client.Lobby.UI.LobbyGui lobby && lobby.CharacterSetupState.Visible)
            {
                foreach (var child in lobby.CharacterSetupState.Children)
                    if (child is Content.Client.Lobby.UI.CharacterSetupGui setup)
                    {
                        setup.HandleOrbitraEscape();
                        return;
                    }
            }
            // Orbitra added end
            _escapeUIController.ToggleWindow();
        }
    }
}
