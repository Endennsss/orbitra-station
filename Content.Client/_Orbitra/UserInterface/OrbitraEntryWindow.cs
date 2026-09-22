using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Opt-in presentation for common windows in both the lobby and the round.</summary>
internal static class OrbitraEntryWindow
{
    /// <summary>Delays only user-requested closure; native Close and Dispose remain immediate.</summary>
    internal static void RequestClose(BaseWindow window)
    {
        foreach (var child in window.Children)
        {
            if (child is Lifetime lifetime)
            {
                lifetime.RequestClose();
                return;
            }
        }
        window.Close();
    }

    internal static bool IsClosing(BaseWindow window)
    {
        foreach (var child in window.Children)
        {
            if (child is Lifetime lifetime)
                return lifetime.Closing;
        }
        return false;
    }

    public static void Attach(BaseWindow window)
    {
        if (window.HasStyleClass("OrbitraEntryWindow"))
            return;
        window.AddStyleClass("OrbitraEntryWindow");
        var keyboard = OrbitraKeyboardNavigation.Attach(window);
        var ui = IoCManager.Resolve<IUserInterfaceManager>();
        var firstOpen = true;
        System.Numerics.Vector2? lastPosition = null;
        var active = false;
        var generation = 0;
        var fitQueued = false;
        Lifetime lifetime = null!;
        void Fit()
        {
            if (active && window.IsOpen)
                OrbitraEditorStyles.FitWindow(window);
        }
        void QueueFit()
        {
            if (fitQueued)
                return;
            fitQueued = true;
            ui.DeferAction(() =>
            {
                fitQueued = false;
                if (!window.Disposed)
                    Fit();
            });
        }
        window.OnOpen += () =>
        {
            var openingPosition = active ? window.Position : lastPosition;
            if (active && !lifetime.Closing)
            {
                // OpenCentered повторно вызывает OnOpen перед штатным центрированием.
                ui.DeferAction(() =>
                {
                    if (!window.Disposed && window.IsOpen && openingPosition is { } position)
                        Robust.Client.UserInterface.Controls.LayoutContainer.SetPosition(window, position);
                });
                return;
            }
            var reversing = lifetime.Closing;
            lifetime.CancelClose();
            keyboard.Opening(reversing);
            // Повторный Open уже открытого окна не дублирует подписки.
            window.OnResized -= QueueFit;
            ui.RootControl.OnResized -= QueueFit;
            active = true;
            var opening = ++generation;
            OrbitraEditorStyles.Apply(window);
            Fit();
            window.OnResized += QueueFit;
            ui.RootControl.OnResized += QueueFit;
            // Прозрачность задаётся до первого кадра, а не после отложенной раскладки.
            OrbitraMotion.Reveal(window, OrbitraMotion.WindowDuration);
            ui.DeferAction(() =>
            {
                if (window.Disposed || !active || !window.IsOpen || opening != generation)
                    return;
                if (!firstOpen && openingPosition is { } position)
                    Robust.Client.UserInterface.Controls.LayoutContainer.SetPosition(window, position);
                OrbitraEditorStyles.FitWindow(window, firstOpen);
                firstOpen = false;
            });
        };
        void Cleanup()
        {
            keyboard.Closing();
            lifetime.CancelClose();
            OrbitraMotion.Finish(window, ui);
            if (active)
                lastPosition = window.Position;
            active = false;
            generation++;
            window.OnResized -= QueueFit;
            ui.RootControl.OnResized -= QueueFit;
        }
        window.OnClose += Cleanup;
        lifetime = new Lifetime(window, Cleanup) { MouseFilter = Control.MouseFilterMode.Ignore };
        window.AddChild(lifetime);
    }

    /// <summary>Also releases root subscriptions when a window is disposed without calling Close.</summary>
    private sealed class Lifetime(BaseWindow window, Action cleanup) : Control
    {
        private readonly OrbitraInputBlock _input = new();
        private int _generation;
        private bool _awaitingClose;
        private Color _closeColor;
        internal bool Closing { get; private set; }

        internal void RequestClose()
        {
            if (Closing || !window.IsOpen || window.Disposed)
                return;
            Closing = true;
            var generation = ++_generation;
            OrbitraMotion.CloseOwnedPopups(window);
            OrbitraKeyboardNavigation.Attach(window).Closing();
            _input.Block(window);
            OrbitraMotion.Hide(window, OrbitraMotion.WindowCloseDuration, () =>
            {
                if (!Closing || generation != _generation || window.Disposed)
                    return;
                _awaitingClose = true;
                _closeColor = window.Modulate;
                window.Modulate = _closeColor.WithAlpha(0);
                // Штатное закрытие меняет дерево UI; выполняем его вне обхода FrameUpdate.
                IoCManager.Resolve<IUserInterfaceManager>().DeferAction(() =>
                {
                    if (Closing && generation == _generation && !window.Disposed)
                        window.Close();
                });
            });
        }

        internal void CancelClose()
        {
            if (_awaitingClose && !window.Disposed)
                window.Modulate = _closeColor;
            _awaitingClose = false;
            Closing = false;
            _generation++;
            _input.Restore();
        }

        protected override void ExitedTree()
        {
            cleanup();
            base.ExitedTree();
        }
    }
}
