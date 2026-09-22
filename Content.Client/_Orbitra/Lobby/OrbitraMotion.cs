using System.Numerics;
using Content.Shared.CCVar;
using Content.Client.Lobby.UI;
using Content.Client.MainMenu.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.Lobby;

/// <summary>Entry-UI transitions, owned by the UI root rather than a static collection of windows.</summary>
internal sealed class OrbitraMotion : Control
{
    internal const float ScreenDuration = 0.20f;
    internal const float WindowDuration = 0.18f;
    internal const float SectionDuration = 0.14f;
    internal const float MenuDuration = 0.10f;
    private readonly IConfigurationManager _configuration = IoCManager.Resolve<IConfigurationManager>();
    private readonly List<Transition> _active = new();
    private readonly List<Surface> _surfaces = new();

    internal int ActiveCount => _active.Count + _surfaces.Count;

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
            selected = index;
            if (index >= 0 && index < tabs.ChildCount)
                Reveal(tabs.GetChild(index), SectionDuration);
        };
    }

    internal static void Reveal(Control target, float duration)
    {
        var runner = GetRunner();
        foreach (var transition in runner._active)
        {
            if (transition.Target != target || !transition.Running)
                continue;
            transition.Reveal(duration);
            return;
        }
        new Transition(target).Reveal(duration);
    }

    internal static void Finish(Control target)
    {
        foreach (var child in IoCManager.Resolve<IUserInterfaceManager>().RootControl.Children)
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
        var surface = new Surface(button);
        void Schedule()
        {
            if (!IsEntryContext())
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
        if (_configuration.GetCVar(CCVars.ReducedMotion) || !IsEntryContext())
            transition.Finish();
        else
            _active.Add(transition);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (ActiveCount == 0)
            return;
        Advance(args.DeltaSeconds, _configuration.GetCVar(CCVars.ReducedMotion) || !IsEntryContext());
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
        foreach (var transition in _active)
            transition.Finish();
        _active.Clear();
        foreach (var surface in _surfaces)
            surface.Finish();
        _surfaces.Clear();
        base.Dispose(disposing);
    }

    /// <summary>One reusable transition. Reversal starts at the current opacity and offset.</summary>
    internal sealed class Transition
    {
        internal readonly Control Target;
        private readonly Color _baseColor;
        private float _from;
        private float _to = 1;
        private float _elapsed;
        private float _duration;
        private Vector2 _offset;
        private Vector2 _fromOffset;
        private Vector2 _targetOffset;
        private Vector2 _size;
        private Vector2 _position;
        private Vector2 _rootSize;
        private float _scale;
        private bool _prepared;
        private bool _hide;
        private bool _pulse;
        internal bool Running { get; private set; }
        internal float Value { get; private set; } = 1;

        internal Transition(Control target)
        {
            Target = target;
            _baseColor = target.Modulate;
        }

        internal void Reveal(float duration, Vector2 offset = default)
        {
            if (Target.Disposed)
                return;
            _hide = _pulse = false;
            _from = Running ? Value : 0;
            _fromOffset = Running ? _offset : offset;
            _targetOffset = Vector2.Zero;
            _to = 1;
            Start(duration);
        }

        internal void Hide(float duration, Vector2 offset)
        {
            _hide = true;
            _pulse = false;
            _from = Value;
            _fromOffset = _offset;
            _targetOffset = offset;
            _to = 0;
            Start(duration);
        }

        internal void Pulse()
        {
            _pulse = true;
            _hide = false;
            _from = 0;
            _to = 1;
            Start(0.40f);
        }

        private void Start(float duration)
        {
            _duration = duration;
            _elapsed = 0;
            _prepared = false;
            Running = true;
            GetRunner().Start(this);
            if (Running && !_pulse)
                Apply(_from, _fromOffset);
        }

        internal void Advance(float seconds, bool reducedMotion)
        {
            if (!Running)
                return;
            if (Target.Disposed || !Target.VisibleInTree || reducedMotion)
            {
                Finish();
                return;
            }
            // Первый кадр даёт штатной раскладке закончить измерение нового содержимого.
            if (!_prepared)
            {
                // Некоторые динамические списки инвалидируют Measure каждый кадр: не ждём «вечной» валидности.
                if (Target.Size == Vector2.Zero)
                {
                    _elapsed += seconds;
                    if (_elapsed >= _duration)
                        Finish();
                    return;
                }
                _size = Target.Size;
                _position = Target.GlobalPosition;
                _rootSize = Target.Root?.Size ?? Vector2.Zero;
                _scale = Target.UIScale;
                _prepared = true;
                return;
            }
            if (_size != Target.Size || _position != Target.GlobalPosition || _scale != Target.UIScale || _rootSize != Target.Root?.Size)
            {
                Finish();
                return;
            }
            _elapsed = Math.Min(_duration, _elapsed + seconds);
            var t = Ease(_elapsed / _duration);
            if (_pulse)
            {
                var emphasis = _elapsed < 0.18f ? Ease(_elapsed / 0.18f) : 1 - Ease((_elapsed - 0.18f) / 0.22f);
                Target.Modulate = Color.InterpolateBetween(_baseColor, new Color(1.18f, 1.18f, 1.18f, _baseColor.A), emphasis);
            }
            else
                Apply(_from + (_to - _from) * t, Vector2.Lerp(_fromOffset, _targetOffset, t));
            if (_elapsed >= _duration)
                Finish();
        }

        internal static float Ease(float value)
        {
            var t = 1 - Math.Clamp(value, 0, 1);
            return 1 - t * t * t;
        }

        private void Apply(float value, Vector2 offset)
        {
            Value = value;
            Target.Modulate = _baseColor.WithAlpha(_baseColor.A * value);
            if (Target is OrbitraMotionHost host)
                host.SetVisualOffset(offset);
            _offset = offset;
        }

        internal void Finish()
        {
            Running = false;
            Value = _hide ? 0 : 1;
            _offset = Vector2.Zero;
            if (Target.Disposed)
                return;
            Target.Modulate = _baseColor;
            if (Target is OrbitraMotionHost host)
            {
                host.SetVisualOffset(Vector2.Zero);
                host.CompleteMotion(_hide);
            }
        }
    }

    private sealed class Surface(ContainerButton button)
    {
        private StyleBoxFlat? _surface;
        private Color _background;
        private Color _border;
        private float _elapsed;
        private BaseButton.DrawModeEnum _mode;

        internal void Restart()
        {
            if (_surface == null && button.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var style) && style is StyleBoxFlat flat)
                _surface = new StyleBoxFlat(flat);
            if (_surface == null)
                return;
            _background = _surface.BackgroundColor;
            _border = _surface.BorderColor;
            _elapsed = 0;
            _mode = button.DrawMode;
        }

        internal bool Advance(float seconds, bool reducedMotion)
        {
            if (button.Disposed || !button.VisibleInTree || reducedMotion)
                return Finish();
            if (_mode != button.DrawMode)
                Restart();
            if (_surface == null || !button.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var style) || style is not StyleBoxFlat target)
                return Finish();
            _elapsed += seconds;
            var duration = button.DrawMode == BaseButton.DrawModeEnum.Pressed ? 0.07f : 0.12f;
            var t = Transition.Ease(_elapsed / duration);
            _surface.BackgroundColor = Color.InterpolateBetween(_background, target.BackgroundColor, t);
            _surface.BorderColor = Color.InterpolateBetween(_border, target.BorderColor, t);
            button.StyleBoxOverride = _surface;
            return _elapsed >= duration && Finish();
        }

        internal bool Finish()
        {
            if (!button.Disposed && button.StyleBoxOverride == _surface)
                button.StyleBoxOverride = null;
            return true;
        }
    }
}
