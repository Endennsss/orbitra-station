using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Log;

namespace Content.Client._Orbitra.UserInterface;

internal sealed partial class OrbitraMotion
{
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
        private Vector2 _rootSize;
        private Control? _screen;
        private float _scale;
        private bool _prepared;
        private bool _presented;
        private float _prepareElapsed;
        private bool _hide;
        private bool _pulse;
        private Action? _completed;
        internal enum Phase { Hidden, Preparing, Appearing, Shown, Disappearing }
        internal Phase State { get; private set; } = Phase.Hidden;
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
            if (Running && !_hide && !_pulse)
            {
                GetRunner().Start(this);
                return;
            }
            _completed = null;
            _hide = _pulse = false;
            _from = Running ? Value : 0;
            _fromOffset = Running ? _offset : offset;
            _targetOffset = Vector2.Zero;
            _to = 1;
            Start(duration);
        }

        internal void Hide(float duration, Vector2 offset = default, Action? completed = null)
        {
            if (Running && _hide)
                return;
            _completed = completed;
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
            _prepared = _hide;
            _presented = false;
            _rootSize = Target.Root?.Size ?? Vector2.Zero;
            _scale = Target.UIScale;
            _prepareElapsed = 0;
            _screen = IoCManager.Resolve<IUserInterfaceManager>().ActiveScreen;
            State = _hide ? Phase.Disappearing : Phase.Preparing;
            Running = true;
            GetRunner().Start(this);
            if (Running && !_pulse)
                Apply(_from, _fromOffset);
        }

        internal void Advance(float seconds, bool reducedMotion)
        {
            if (!Running)
                return;
            if (Target.Disposed || !Target.VisibleInTree || reducedMotion || !CanAnimate(Target) ||
                _screen != IoCManager.Resolve<IUserInterfaceManager>().ActiveScreen)
            {
                Finish();
                return;
            }
            // Подготовка не расходует длительность эффекта. Перераскладка содержимого
            // после подготовки не является пользовательским изменением размера окна.
            if (!_prepared)
            {
                _prepareElapsed += seconds;
                if (!_presented)
                {
                    if (_prepareElapsed >= 0.5f)
                    {
                        Logger.DebugS("orbitra.ui.motion", $"Skipped reveal for {Target.GetType().Name}: layout preparation exceeded 500 ms.");
                        Finish();
                    }
                    return;
                }
                _prepared = true;
                State = _hide ? Phase.Disappearing : Phase.Appearing;
            }
            if (_scale != Target.UIScale || _rootSize != Target.Root?.Size)
            {
                Finish();
                return;
            }
            _elapsed = Math.Min(_duration, _elapsed + Math.Clamp(seconds, 0, 0.05f));
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

        internal void Presented()
        {
            if (!Running || _prepared || !Target.VisibleInTree || Target.Width <= 0 || Target.Height <= 0)
                return;
            _rootSize = Target.Root?.Size ?? Vector2.Zero;
            _scale = Target.UIScale;
            _presented = true;
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
            var completed = _completed;
            _completed = null;
            Running = false;
            State = _hide ? Phase.Hidden : Phase.Shown;
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
            completed?.Invoke();
        }
    }

}
