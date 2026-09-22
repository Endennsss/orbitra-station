using Content.Client.Changelog;
using JetBrains.Annotations;
using Robust.Client.State;
using Robust.Client.UserInterface.Controllers;

using Content.Client._Orbitra.UserInterface; // Orbitra-Edit

namespace Content.Client.UserInterface.Systems.EscapeMenu;

[UsedImplicitly]
public sealed class ChangelogUIController : UIController
{
    private ChangelogWindow _changeLogWindow = default!;

    public void OpenWindow()
    {
        EnsureWindow();

        _changeLogWindow.OpenCentered();
        _changeLogWindow.MoveToFront();
    }

    private void EnsureWindow()
    {
        if (_changeLogWindow is { Disposed: false })
            return;

        _changeLogWindow = UIManager.CreateWindow<ChangelogWindow>();
    }

    public void ToggleWindow()
    {
        EnsureWindow();

        if (_changeLogWindow.IsOpen && !Content.Client._Orbitra.UserInterface.OrbitraEntryWindow.IsClosing(_changeLogWindow)) // Orbitra-Edit
        {
            Content.Client._Orbitra.UserInterface.OrbitraEntryWindow.RequestClose(_changeLogWindow); // Orbitra-Edit
        }
        else
        {
            OpenWindow();
        }
    }
}
