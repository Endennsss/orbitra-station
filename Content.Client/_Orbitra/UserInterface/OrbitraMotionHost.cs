using System.Numerics;
using Robust.Client.UserInterface;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Moves content inside its final layout slot without changing the slot's measurement.</summary>
public sealed class OrbitraMotionHost : Control
{
    private Vector2 _offset;
    private OrbitraMotion.Transition? _transition;
    private bool _requestedVisible;
    private readonly List<(Control Control, MouseFilterMode Mouse, bool Focus)> _blocked = new();

    internal void Reveal(float duration, Vector2 offset)
    {
        _requestedVisible = true;
        RestoreInput();
        Visible = true;
        (_transition ??= new OrbitraMotion.Transition(this)).Reveal(duration, offset);
    }

    internal void SetShown(bool shown, Vector2 offset, bool immediate = false)
    {
        if (_requestedVisible == shown && !immediate)
            return;
        _requestedVisible = shown;
        if (immediate)
        {
            _transition?.Finish();
            RestoreInput();
            Visible = shown;
            return;
        }
        if (shown)
            Reveal(0.18f, offset);
        else if (Visible)
        {
            BlockInput(this);
            (_transition ??= new OrbitraMotion.Transition(this)).Hide(0.12f, offset);
        }
    }

    private void BlockInput(Control control)
    {
        if (_blocked.Count > 0 && control == this)
            return;
        _blocked.Add((control, control.MouseFilter, control.CanKeyboardFocus));
        control.MouseFilter = MouseFilterMode.Ignore;
        control.CanKeyboardFocus = false;
        control.OnChildAdded += BlockInput;
        foreach (var child in control.Children)
            BlockInput(child);
    }

    private void RestoreInput()
    {
        foreach (var (control, mouse, focus) in _blocked)
        {
            if (control.Disposed)
                continue;
            control.OnChildAdded -= BlockInput;
            control.MouseFilter = mouse;
            control.CanKeyboardFocus = focus;
        }
        _blocked.Clear();
    }

    internal void CompleteMotion(bool hidden)
    {
        if (hidden)
            Visible = false;
        RestoreInput();
    }

    internal void SetVisualOffset(Vector2 offset)
    {
        if (_offset == offset)
            return;
        _offset = offset;
        InvalidateArrange();
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var child in Children)
            child.Arrange(UIBox2.FromDimensions(_offset, finalSize));
        return finalSize;
    }

    protected override void ExitedTree()
    {
        _transition?.Finish();
        RestoreInput();
        base.ExitedTree();
    }
}
