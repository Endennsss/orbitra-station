using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Emote-only sector with native hit testing and smoothly changing surface colors.</summary>
public sealed class OrbitraRadialSector : RadialMenuButtonWithSector
{
    private Color _from;
    private Color _to;
    private float _elapsed;

    public OrbitraRadialSector()
    {
        AddStyleClass("OrbitraRadialSector");
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();
        UpdateSurface(false);
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateSurface(true);
    }

    private void UpdateSurface(bool animate)
    {
        var property = DrawMode switch
        {
            DrawModeEnum.Hover => "orbitra-radial-hover",
            DrawModeEnum.Pressed => "orbitra-radial-pressed",
            DrawModeEnum.Disabled => "orbitra-radial-disabled",
            _ => "orbitra-radial-normal",
        };
        if (!TryGetStyleProperty<Color>(property, out var target))
            return;
        if (target == _to)
            return;
        _from = BackgroundColor;
        _to = target;
        _elapsed = 0;
        if (TryGetStyleProperty<Color>("orbitra-radial-border", out var border))
            BorderColor = HoverBorderColor = SeparatorColor = border;
        if (animate && VisibleInTree)
            OrbitraMotion.AnimateRadial(this);
        else
            FinishSurface();
    }

    internal bool AdvanceSurface(float seconds, bool reduced)
    {
        if (Disposed)
            return true;
        if (reduced || !VisibleInTree)
        {
            FinishSurface();
            return true;
        }
        _elapsed = Math.Min(OrbitraMotion.MenuDuration, _elapsed + Math.Clamp(seconds, 0, 0.05f));
        BackgroundColor = HoverBackgroundColor = Color.InterpolateBetween(_from, _to,
            OrbitraMotion.Transition.Ease(_elapsed / OrbitraMotion.MenuDuration));
        return _elapsed >= OrbitraMotion.MenuDuration;
    }

    internal void FinishSurface()
    {
        if (!Disposed)
            BackgroundColor = HoverBackgroundColor = _to;
    }

    protected override void ExitedTree()
    {
        OrbitraMotion.RemoveRadial(this);
        base.ExitedTree();
    }
}
