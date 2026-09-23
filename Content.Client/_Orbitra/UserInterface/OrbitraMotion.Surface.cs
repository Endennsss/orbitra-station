using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Log;

namespace Content.Client._Orbitra.UserInterface;

internal sealed partial class OrbitraMotion
{
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
            if (button.Disposed || !button.VisibleInTree || reducedMotion || !CanAnimate(button))
                return Finish();
            if (_mode != button.DrawMode)
                Restart();
            if (_surface == null || !button.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var style) || style is not StyleBoxFlat target)
                return Finish();
            _elapsed += seconds;
            var duration = button.DrawMode == BaseButton.DrawModeEnum.Pressed ? OrbitraMotionPresets.Press : OrbitraMotionPresets.Hover;
            var t = Transition.Ease(_elapsed / duration);
            // Выделение не должно наследовать геометрию рамки предыдущего состояния.
            _surface.BorderThickness = target.BorderThickness;
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
