using Robust.Client.UserInterface;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Temporarily removes input from a fading subtree, including newly added children.</summary>
internal sealed class OrbitraInputBlock
{
    private readonly List<(Control Control, Control.MouseFilterMode Mouse, bool Focus)> _controls = new();

    internal void Block(Control control)
    {
        _controls.Add((control, control.MouseFilter, control.CanKeyboardFocus));
        control.ReleaseKeyboardFocus();
        control.MouseFilter = Control.MouseFilterMode.Ignore;
        control.CanKeyboardFocus = false;
        control.OnChildAdded += Block;
        foreach (var child in control.Children)
            Block(child);
    }

    internal void Restore()
    {
        foreach (var (control, mouse, focus) in _controls)
        {
            control.OnChildAdded -= Block;
            if (control.Disposed)
                continue;
            control.MouseFilter = mouse;
            control.CanKeyboardFocus = focus;
        }
        _controls.Clear();
    }
}
