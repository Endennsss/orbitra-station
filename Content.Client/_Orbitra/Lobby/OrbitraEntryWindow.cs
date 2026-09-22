using Content.Client.Lobby.UI;
using Content.Client.MainMenu.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Orbitra.Lobby;

/// <summary>Opt-in sizing for entry-flow windows; unrelated windows are never intercepted.</summary>
internal static class OrbitraEntryWindow
{
    public static void Attach(BaseWindow window)
    {
        if (window.HasStyleClass("OrbitraEntryWindow"))
            return;
        window.AddStyleClass("OrbitraEntryWindow");
        var ui = IoCManager.Resolve<IUserInterfaceManager>();
        var firstOpen = true;
        System.Numerics.Vector2? lastPosition = null;
        var active = false;
        void Fit()
        {
            if (active && window.IsOpen)
                OrbitraEditorStyles.FitWindow(window);
        }
        void QueueFit() => ui.DeferAction(Fit);
        window.OnOpen += () =>
        {
            active = ui.ActiveScreen is LobbyGui;
            foreach (var child in ui.StateRoot.Children)
                active |= child is MainMenuControl;
            if (!active)
                return;
            OrbitraEditorStyles.Apply(window);
            Fit();
            window.OnResized += QueueFit;
            ui.RootControl.OnResized += QueueFit;
            ui.DeferAction(() =>
            {
                if (!active || !window.IsOpen)
                    return;
                if (!firstOpen && lastPosition is { } position)
                    Robust.Client.UserInterface.Controls.LayoutContainer.SetPosition(window, position);
                OrbitraEditorStyles.FitWindow(window, firstOpen);
                firstOpen = false;
                OrbitraMotion.Reveal(window, OrbitraMotion.WindowDuration);
            });
        };
        window.OnClose += () =>
        {
            OrbitraMotion.Finish(window);
            if (active)
                lastPosition = window.Position;
            active = false;
            window.OnResized -= QueueFit;
            ui.RootControl.OnResized -= QueueFit;
        };
    }
}
