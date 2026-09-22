using Content.Client._Orbitra.Lobby;

using Content.Client._Orbitra.UserInterface; // Orbitra-Edit

namespace Content.Client.MainMenu.UI;

public sealed partial class MainMenuControl
{
    protected override void EnteredTree()
    {
        base.EnteredTree();
        UserInterfaceManager.DeferAction(() =>
        {
            if (!Disposed && VisibleInTree)
                OrbitraMotion.Reveal(OrbitraMenuScroll, OrbitraMotion.ScreenDuration);
        });
    }
}
