using System.Numerics;
using Content.Shared.CCVar;
using Content.Client.Lobby.UI;
using Content.Client.MainMenu.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Log;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Entry-UI transitions, owned by the UI root rather than a static collection of windows.</summary>
internal sealed partial class OrbitraMotion : Control
{
    internal const float ScreenDuration = 0.24f;
    internal const float WindowDuration = OrbitraMotionPresets.WindowOpen;
    internal const float WindowCloseDuration = OrbitraMotionPresets.WindowClose;
    internal const float SectionDuration = OrbitraMotionPresets.Section;
    internal const float MenuDuration = OrbitraMotionPresets.PopupOpen;
    internal const float MenuCloseDuration = OrbitraMotionPresets.PopupClose;
    internal static void BindPopup(Control popup, Control owner, Action? hidden = null)
    {
        foreach (var child in popup.Children)
        {
            if (child is PopupOwner link)
            {
                link.Owner = owner;
                return;
            }
        }
        var linkControl = new PopupOwner(owner) { MouseFilter = MouseFilterMode.Ignore };
        popup.AddChild(linkControl);
        OrbitraKeyboardNavigation.Attach(popup);
        if (popup is Popup menu)
            linkControl.Presentation = new OrbitraPopupPresentation(menu, owner, linkControl, hidden);
    }

    internal static void FinishPopup(Popup popup)
    {
        foreach (var child in popup.Children)
        {
            if (child is PopupOwner link)
            {
                link.Presentation?.Restore();
                return;
            }
        }
    }

    internal static bool CanAnimate(Control target)
    {
        for (Control? current = target; current != null; current = current.Parent)
        {
            if (current is OrbitraTooltip { Owner: { } owner })
                return owner.VisibleInTree && !owner.Disposed && CanAnimate(owner);
            if (current.HasStyleClass("OrbitraEntryWindow"))
                return current.VisibleInTree && !current.Disposed;
            if (current is Robust.Client.UserInterface.CustomControls.BaseWindow)
                return false;
            if (current is Popup)
            {
                foreach (var child in current.Children)
                {
                    if (child is PopupOwner link)
                        return link.Owner.VisibleInTree && !link.Owner.Disposed && CanAnimate(link.Owner);
                }
            }
        }
        return IsEntryContext();
    }

    internal static void CloseOwnedPopups(Control owner)
    {
        var root = IoCManager.Resolve<IUserInterfaceManager>().ModalRoot;
        var popups = new List<Popup>();
        foreach (var child in root.Children)
        {
            if (child is not Popup popup)
                continue;
            foreach (var marker in popup.Children)
            {
                if (marker is not PopupOwner link)
                    continue;
                for (Control? current = link.Owner; current != null; current = current.Parent)
                {
                    if (current != owner)
                        continue;
                    popups.Add(popup);
                    break;
                }
            }
        }
        foreach (var popup in popups)
            popup.Close();
    }

    private sealed class PopupOwner(Control owner) : Control
    {
        internal Control Owner = owner;
        internal OrbitraPopupPresentation? Presentation;

        protected override Vector2 MeasureOverride(Vector2 availableSize) => Vector2.Zero;

        protected override void Dispose(bool disposing)
        {
            Presentation?.Dispose();
            base.Dispose(disposing);
        }
    }
    private readonly IConfigurationManager _configuration = IoCManager.Resolve<IConfigurationManager>();
    private readonly List<Transition> _active = new();
    private readonly List<Surface> _surfaces = new();

    internal int ActiveCount => _active.Count + _surfaces.Count;

    public OrbitraMotion()
    {
        _configuration.OnValueChanged(CCVars.ReducedMotion, OnReducedMotion);
    }

    private void OnReducedMotion(bool reduced)
    {
        if (reduced)
            Advance(0, true);
    }

    private static OrbitraMotion GetRunner()
    {
        var root = IoCManager.Resolve<IUserInterfaceManager>().RootControl;
        foreach (var child in root.Children)
        {
            if (child is OrbitraMotion runner)
                return runner;
        }
        var result = new OrbitraMotion { MouseFilter = MouseFilterMode.Ignore };
        root.AddChild(result);
        return result;
    }

    /// <summary>Attaches a reveal to actual visibility changes, never to data refreshes.</summary>
    internal static void AttachReveal(Control target, float duration)
    {
        if (target.HasStyleClass("OrbitraMotionAttached"))
            return;
        target.AddStyleClass("OrbitraMotionAttached");
        var transition = new Transition(target);
        target.OnVisibilityChanged += _ =>
        {
            if (target.VisibleInTree)
                transition.Reveal(duration);
            else
                transition.Finish();
        };
        if (target.VisibleInTree)
            transition.Reveal(duration);
    }

    /// <summary>Animates only selected tab content, not tab headers or virtualized rows.</summary>
    internal static void AttachTabs(TabContainer tabs)
    {
        var selected = tabs.CurrentTab;
        tabs.OnTabChanged += index =>
        {
            if (selected == index)
                return;
            if (selected >= 0 && selected < tabs.ChildCount)
                Finish(tabs.GetChild(selected));
            selected = index;
            if (index >= 0 && index < tabs.ChildCount)
                Reveal(tabs.GetChild(index), SectionDuration);
        };
    }

    internal static void Reveal(Control target, float duration)
    {
        var runner = GetRunner();
        foreach (var active in runner._active)
        {
            if (!active.Running)
                continue;
            for (var parent = target.Parent; parent != null; parent = parent.Parent)
            {
                if (active.Target == parent)
                    return;
            }
        }
        foreach (var transition in runner._active)
        {
            if (transition.Target != target || !transition.Running)
                continue;
            transition.Reveal(duration);
            return;
        }
        for (var i = runner._active.Count - 1; i >= 0; i--)
        {
            for (var parent = runner._active[i].Target.Parent; parent != null; parent = parent.Parent)
            {
                if (parent != target)
                    continue;
                runner._active[i].Finish();
                runner._active.RemoveAt(i);
                break;
            }
        }
        new Transition(target).Reveal(duration);
    }

    internal static void Hide(Control target, float duration, Action completed)
    {
        var runner = GetRunner();
        foreach (var transition in runner._active)
        {
            if (transition.Target != target || !transition.Running)
                continue;
            transition.Hide(duration, completed: completed);
            return;
        }
        new Transition(target).Hide(duration, completed: completed);
    }

    internal static void Finish(Control target, IUserInterfaceManager? ui = null)
    {
        foreach (var child in (ui ?? IoCManager.Resolve<IUserInterfaceManager>()).RootControl.Children)
        {
            if (child is OrbitraMotion runner)
                runner.Cancel(target);
        }
    }

    private static bool IsEntryContext()
    {
        var ui = IoCManager.Resolve<IUserInterfaceManager>();
        if (ui.ActiveScreen is LobbyGui)
            return true;
        foreach (var child in ui.StateRoot.Children)
        {
            if (child is MainMenuControl)
                return true;
        }
        return false;
    }

    private void Cancel(Control target)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].Target != target)
                continue;
            _active[i].Finish();
            _active.RemoveAt(i);
        }
    }

    /// <summary>Briefly emphasizes an existing status without changing its text or geometry.</summary>
    internal static void Pulse(Control target)
    {
        if (target.VisibleInTree)
        {
            GetRunner().Cancel(target);
            new Transition(target).Pulse();
        }
    }

    /// <summary>Opt-in color transitions for existing buttons; native input remains unchanged.</summary>
    internal static void AttachButton(ContainerButton button)
    {
        if (button.HasStyleClass("OrbitraSurfaceAttached"))
            return;
        button.AddStyleClass("OrbitraSurfaceAttached");
        button.CanKeyboardFocus = true;
        OrbitraFocusRing.Attach(button);
        var surface = new Surface(button);
        void Schedule()
        {
            if (!CanAnimate(button))
                return;
            var runner = GetRunner();
            surface.Restart();
            if (!runner._surfaces.Contains(surface))
                runner._surfaces.Add(surface);
        }
        button.OnMouseEntered += _ => Schedule();
        button.OnMouseExited += _ => Schedule();
        button.OnKeyBindDown += _ => Schedule();
        button.OnKeyBindUp += _ => Schedule();
        button.OnToggled += _ => Schedule();
    }

    private void Start(Transition transition)
    {
        if (_configuration.GetCVar(CCVars.ReducedMotion) || !CanAnimate(transition.Target))
        {
            transition.Finish();
            return;
        }
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i] == transition)
                return;
            if (_active[i].Target == transition.Target)
            {
                _active[i].Finish();
                _active.RemoveAt(i);
            }
        }
        _active.Add(transition);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (ActiveCount == 0)
            return;
        Advance(args.DeltaSeconds, _configuration.GetCVar(CCVars.ReducedMotion));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        Presented();
    }

    /// <summary>Records a rendered UI frame separately from layout and simulation time.</summary>
    internal void Presented()
    {
        foreach (var transition in _active)
            transition.Presented();
    }

    internal void Advance(float seconds, bool reducedMotion)
    {
        for (var i = _surfaces.Count - 1; i >= 0; i--)
        {
            if (_surfaces[i].Advance(seconds, reducedMotion))
                _surfaces.RemoveAt(i);
        }
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var transition = _active[i];
            transition.Advance(seconds, reducedMotion);
            if (!transition.Running)
                _active.RemoveAt(i);
        }
    }

    protected override void Dispose(bool disposing)
    {
        _configuration.UnsubValueChanged(CCVars.ReducedMotion, OnReducedMotion);
        foreach (var transition in _active)
            transition.Finish();
        _active.Clear();
        foreach (var surface in _surfaces)
            surface.Finish();
        _surfaces.Clear();
        base.Dispose(disposing);
    }

}
