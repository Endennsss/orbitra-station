using System.Numerics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Map;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Keyboard traversal restricted to an explicitly registered presentation root.</summary>
internal sealed class OrbitraKeyboardNavigation : Control
{
    internal static bool Rebinding;
    private static int _keyboardDispatch;
    internal static Keyboard.Key? ActivationKey { get; private set; }
    internal Func<KeyEventArgs, bool>? HandleKey;
    private readonly Control _scope;
    private readonly IInputManager _input;
    private readonly List<Control> _stops = new();
    private Control? _returnFocus;
    private Control? _lastFocus;
    private BaseButton? _pressed;
    private Keyboard.Key _pressedKey;
    private bool _focusReturned;

    internal static void Activate(IOrbitraKeyboardButton button, bool down, Keyboard.Key key)
    {
        var ui = IoCManager.Resolve<IUserInterfaceManager>();
        var control = (Control) button;
        var position = control.GlobalPixelPosition + control.PixelSize / 2;
        _keyboardDispatch++;
        ActivationKey = key;
        try
        {
            button.KeyboardActivate(new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick,
                down ? BoundKeyState.Down : BoundKeyState.Up,
                new ScreenCoordinates(position, ui.MousePositionScaled.Window), false, control.Size / 2, control.PixelSize / 2));
        }
        finally
        {
            _keyboardDispatch--;
            ActivationKey = null;
        }
    }

    internal void Opening(bool reversing)
    {
        _focusReturned = false;
        if (reversing && _lastFocus is { } previous && Available(previous))
            Focus(previous);
        else if (!Contains(_scope, UserInterfaceManager.KeyboardFocused))
            _returnFocus = UserInterfaceManager.KeyboardFocused;
        if (!reversing && _keyboardDispatch > 0)
            UserInterfaceManager.DeferAction(() =>
            {
                if (!Active())
                    return;
                _stops.Clear();
                Collect(_scope);
                foreach (var stop in _stops)
                    if (stop is not OrbitraWindowCloseButton)
                    {
                        Focus(stop);
                        break;
                    }
            });
    }

    internal void Closing()
    {
        CancelPress();
        if (_focusReturned)
            return;
        _focusReturned = true;
        if (UserInterfaceManager.KeyboardFocused != null && !Contains(_scope, UserInterfaceManager.KeyboardFocused))
            return;
        _lastFocus = UserInterfaceManager.KeyboardFocused ?? _lastFocus;
        UserInterfaceManager.ReleaseKeyboardFocus();
        if (_returnFocus is { } previous && Available(previous))
        {
            Focus(previous);
            return;
        }
        Control? fallback = null;
        foreach (var child in UserInterfaceManager.WindowRoot.Children)
            if (child != _scope && child.VisibleInTree && child is BaseWindow window && !OrbitraEntryWindow.IsClosing(window))
                fallback = child;
        if (fallback == null)
            return;
        _stops.Clear();
        Collect(fallback);
        if (_stops.Count > 0)
            Focus(_stops[0]);
    }

    private OrbitraKeyboardNavigation(Control scope)
    {
        _scope = scope;
        _input = IoCManager.Resolve<IInputManager>();
        MouseFilter = MouseFilterMode.Ignore;
    }

    internal static OrbitraKeyboardNavigation Attach(Control scope)
    {
        foreach (var child in scope.Children)
            if (child is OrbitraKeyboardNavigation navigation)
                return navigation;
        var result = new OrbitraKeyboardNavigation(scope);
        scope.AddChild(result);
        return result;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize) => Vector2.Zero;
    protected override void EnteredTree()
    {
        base.EnteredTree();
        _input.FirstChanceOnKeyEvent += OnKey;
    }

    protected override void ExitedTree()
    {
        CancelPress();
        _input.FirstChanceOnKeyEvent -= OnKey;
        base.ExitedTree();
    }

    private void CancelPress()
    {
        if (_pressed != null && UserInterfaceManager.ControlFocused == _pressed)
            UserInterfaceManager.ControlFocused = null;
        _pressed = null;
    }

    internal static bool Contains(Control root, Control? control)
    {
        for (; control != null; control = control.Parent)
            if (control == root)
                return true;
        return false;
    }

    internal static bool Available(Control control) => !control.Disposed && control.VisibleInTree &&
        control.CanKeyboardFocus && control is not BaseButton { Disabled: true } &&
        control is not Slider { Disabled: true };

    internal static void Focus(Control control)
    {
        if (!Available(control))
            return;
        control.GrabKeyboardFocus();
        for (var parent = control.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is not ScrollContainer scroll)
                continue;
            var top = control.GlobalPosition.Y - scroll.GlobalPosition.Y;
            var bottom = top + control.Height;
            var delta = top < 0 ? top : bottom > scroll.Height ? bottom - scroll.Height : 0;
            var left = control.GlobalPosition.X - scroll.GlobalPosition.X;
            var right = left + control.Width;
            var horizontal = left < 0 ? left : right > scroll.Width ? right - scroll.Width : 0;
            scroll.SetScrollValue(scroll.GetScrollValue() + new Vector2(horizontal, delta));
        }
    }

    private bool Active()
    {
        if (!_scope.VisibleInTree || _scope.Disposed ||
            _scope is BaseWindow window && OrbitraEntryWindow.IsClosing(window))
            return false;
        for (var focused = UserInterfaceManager.KeyboardFocused; focused != null && focused != _scope; focused = focused.Parent)
            foreach (var child in focused.Children)
                if (child is OrbitraKeyboardNavigation other && other != this && Contains(_scope, focused))
                    return false;
        // Модальность и посторонние окна всегда имеют приоритет над экраном под ними.
        Control? top = null;
        foreach (var child in UserInterfaceManager.ModalRoot.Children)
            if (child.VisibleInTree && (child is Popup || child.MouseFilter != MouseFilterMode.Ignore))
                top = child;
        if (top != null)
            return Contains(_scope, top) || top == _scope;
        foreach (var child in UserInterfaceManager.WindowRoot.Children)
            if (child.VisibleInTree && child is BaseWindow candidate && !OrbitraEntryWindow.IsClosing(candidate))
                top = child;
        return top == null || top == _scope;
    }

    private void Collect(Control control)
    {
        if (!control.VisibleInTree || control.Disposed)
            return;
        if (Available(control))
            _stops.Add(control);
        foreach (var child in control.Children)
            Collect(child);
    }

    private void OnKey(KeyEventArgs args, KeyEventType type)
    {
        if (type == KeyEventType.Up && _pressed != null && args.Key == _pressedKey)
        {
            var pressed = _pressed;
            _pressed = null;
            if (Available(pressed) && pressed.HasKeyboardFocus() && Active() && pressed is IOrbitraKeyboardButton button)
                Activate(button, false, args.Key);
            else if (UserInterfaceManager.ControlFocused == pressed)
                UserInterfaceManager.ControlFocused = null;
            args.Handle();
            return;
        }
        if (args.Handled || Rebinding || type == KeyEventType.Up || !Active())
            return;
        if (HandleKey?.Invoke(args) == true)
        {
            args.Handle();
            return;
        }
        if (args.Control || args.Alt || args.System)
            return;
        if (args.Key is Keyboard.Key.Left or Keyboard.Key.Right or Keyboard.Key.Up or Keyboard.Key.Down &&
            UserInterfaceManager.KeyboardFocused is BaseButton tab && tab.Parent != null &&
            (tab.HasStyleClass("OrbitraNavigationButton") || tab.HasStyleClass("OrbitraJournalTab")))
        {
            _stops.Clear();
            foreach (var child in tab.Parent.Children)
                if (child is BaseButton && Available(child))
                    _stops.Add(child);
            var index = _stops.IndexOf(tab);
            if (index >= 0)
                Focus(_stops[Math.Clamp(index + (args.Key is Keyboard.Key.Left or Keyboard.Key.Up ? -1 : 1), 0, _stops.Count - 1)]);
            args.Handle();
            return;
        }
        if (args.Key is Keyboard.Key.Return or Keyboard.Key.Space &&
            UserInterfaceManager.KeyboardFocused is BaseButton focused && Contains(_scope, focused) &&
            Available(focused) && focused is IOrbitraKeyboardButton keyboardButton)
        {
            if (!args.IsRepeat && _pressed == null)
            {
                _pressed = focused;
                _pressedKey = args.Key;
                Activate(keyboardButton, true, args.Key);
            }
            args.Handle();
        }
        else if (args.Key == Keyboard.Key.Tab && !args.Control && !args.Alt)
        {
            _stops.Clear();
            Collect(_scope);
            if (_stops.Count > 0)
            {
                var index = _stops.IndexOf(UserInterfaceManager.KeyboardFocused!);
                index = index < 0 ? (args.Shift ? _stops.Count - 1 : 0) :
                    (index + (args.Shift ? -1 : 1) + _stops.Count) % _stops.Count;
                Focus(_stops[index]);
            }
            args.Handle();
        }
        else if (UserInterfaceManager.KeyboardFocused is Slider slider && Contains(_scope, slider) && !slider.Disabled)
        {
            var step = slider.Rounded ? MathF.Pow(10, -slider.RoundingDecimals) : (slider.MaxValue - slider.MinValue) / 100;
            switch (args.Key)
            {
                case Keyboard.Key.Left: case Keyboard.Key.Down: slider.Value -= step; break;
                case Keyboard.Key.Right: case Keyboard.Key.Up: slider.Value += step; break;
                case Keyboard.Key.Home: slider.Value = slider.MinValue; break;
                case Keyboard.Key.End: slider.Value = slider.MaxValue; break;
                default: return;
            }
            args.Handle();
        }
    }
}
