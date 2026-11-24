using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Общий жизненный цикл окон со штатной темой и специальной раскладкой.</summary>
internal static class OrbitraEntryWindow
{
    /// <summary>Анимирует закрытие, запрошенное игроком. Штатные Close и Dispose работают сразу.</summary>
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

    internal static void FitViewport(BaseWindow window, float inset, bool center)
    {
        foreach (var child in window.Children)
        {
            if (child is not Lifetime lifetime)
                continue;

            lifetime.Viewport.Fit(inset, center);
            return;
        }
    }

    public static void Attach(BaseWindow window, bool applyEditorLayout = true)
    {
        if (window.HasStyleClass("OrbitraEntryWindow"))
            return;
        window.AddStyleClass("OrbitraEntryWindow");
        var keyboard = OrbitraKeyboardNavigation.Attach(window);
        var ui = IoCManager.Resolve<IUserInterfaceManager>();
        OrbitraEngineMenus.Ensure(ui);
        var firstOpen = true;
        System.Numerics.Vector2? lastPosition = null;
        var active = false;
        var generation = 0;
        var fitQueued = false;
        Lifetime lifetime = null!;
        void Fit()
        {
            if (!active || !window.IsOpen)
                return;
            if (applyEditorLayout)
                OrbitraEditorStyles.FitWindow(window);
            else
                lifetime.Viewport.Fit();
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
                if (!applyEditorLayout)
                {
                    QueueFit();
                    return;
                }
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
            if (applyEditorLayout)
            {
                OrbitraEditorStyles.Apply(window);
                Fit();
            }
            else
            {
                OrbitraWindowTheme.Apply(window);
                QueueFit();
            }
            window.OnResized += QueueFit;
            ui.RootControl.OnResized += QueueFit;
            // Прозрачность задаётся до первого кадра, а не после отложенной раскладки.
            OrbitraMotion.Reveal(window, OrbitraMotion.WindowDuration);
            if (!applyEditorLayout)
                return;
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
            OrbitraTooltips.ClearOwned(window);
            OrbitraNotification.ClearOwned(window);
            keyboard.Closing();
            lifetime.CancelClose();
            OrbitraMotion.Finish(window, ui);
            if (active)
            {
                lastPosition = window.Position;
                lifetime.Viewport.Restore();
            }
            active = false;
            generation++;
            window.OnResized -= QueueFit;
            ui.RootControl.OnResized -= QueueFit;
        }
        window.OnClose += Cleanup;
        lifetime = new Lifetime(window, Cleanup) { MouseFilter = Control.MouseFilterMode.Ignore };
        window.AddChild(lifetime);
    }

    /// <summary>Снимает подписки с корня и при уничтожении окна через Dispose.</summary>
    private sealed class Lifetime(BaseWindow window, Action cleanup) : Control
    {
        private readonly OrbitraInputBlock _input = new();
        private int _generation;
        private bool _awaitingClose;
        private Color _closeColor;
        internal bool Closing { get; private set; }
        internal OrbitraWindowViewport Viewport { get; } = new(window);

        internal void RequestClose()
        {
            if (Closing || !window.IsOpen || window.Disposed)
                return;
            Closing = true;
            OrbitraTooltips.ClearOwned(window);
            OrbitraNotification.ClearOwned(window);
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
