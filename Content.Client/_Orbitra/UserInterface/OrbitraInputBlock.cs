using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Отключает ввод в скрываемой ветке интерфейса, включая добавляемые контролы.</summary>
internal sealed class OrbitraInputBlock
{
    private readonly Dictionary<Control, (Control.MouseFilterMode Mouse, bool Focus)> _controls = new();

    internal void Block(Control control)
    {
        // Перенос внутри ветки не меняет состояние, которое надо восстановить.
        if (!_controls.TryAdd(control, (control.MouseFilter, control.CanKeyboardFocus)))
            return;
        control.ReleaseKeyboardFocus();
        control.MouseFilter = Control.MouseFilterMode.Ignore;
        control.CanKeyboardFocus = false;
        control.OnChildAdded += Block;
        foreach (var child in control.Children)
            Block(child);
    }

    internal void Restore()
    {
        foreach (var (control, (mouse, focus)) in _controls)
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
