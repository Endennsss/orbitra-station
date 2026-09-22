using System.Linq;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Keyboard input for native option rows, sharing explicitly registered selection handlers.</summary>
public sealed class OrbitraOptionButton : OptionButton, IOrbitraKeyboardButton
{
    private readonly List<(Button Button, int Id)> _rows = new();
    private event Action<ItemSelectedEventArgs>? KeyboardSelected;

    public OrbitraOptionButton()
    {
        CanKeyboardFocus = true;
        OrbitraFocusRing.Attach(this);
        OnPressed += _ =>
        {
            if (OptionsScroll.Parent is Popup popup)
                OrbitraKeyboardNavigation.Attach(popup).HandleKey = HandleOptionKey;
        };
    }

    /// <summary>Uses one application handler for both mouse and keyboard selection.</summary>
    public static void BindSelection(OptionButton option, Action<ItemSelectedEventArgs> handler)
    {
        option.OnItemSelected += handler;
        if (option is OrbitraOptionButton keyboard)
            keyboard.KeyboardSelected += handler;
    }

    public override void ButtonOverride(Button button)
    {
        base.ButtonOverride(button);
        _rows.RemoveAll(row => row.Button.Disposed);
        _rows.Add((button, GetItemId(ItemCount - 1)));
    }

    public void KeyboardActivate(GUIBoundKeyEventArgs args)
    {
        if (args.State == BoundKeyState.Down)
            base.KeyBindDown(args);
        else
        {
            base.KeyBindUp(args);
            if (OptionsScroll.Parent is not Popup { Visible: true } popup)
                return;
            OrbitraKeyboardNavigation.Attach(popup).HandleKey = HandleOptionKey;
            var rows = _rows.Where(row => row.Button.VisibleInTree && !row.Button.Disabled).ToList();
            var selected = rows.FindIndex(row => row.Id == SelectedId);
            if (rows.Count > 0)
                OrbitraKeyboardNavigation.Focus(rows[Math.Max(0, selected)].Button);
        }
    }

    private bool HandleOptionKey(KeyEventArgs args)
    {
        if (OptionsScroll.Parent is not Popup popup)
            return false;
        if (args.Key == Keyboard.Key.Escape)
        {
            popup.Close();
            OrbitraKeyboardNavigation.Focus(this);
            return true;
        }
        var rows = _rows.Where(row => row.Button.VisibleInTree && !row.Button.Disabled).ToList();
        if (rows.Count == 0)
            return false;
        var index = rows.FindIndex(row => row.Button.HasKeyboardFocus());
        if (args.Key is Keyboard.Key.Up or Keyboard.Key.Down or Keyboard.Key.Home or Keyboard.Key.End)
        {
            index = args.Key switch
            {
                Keyboard.Key.Home => 0,
                Keyboard.Key.End => rows.Count - 1,
                Keyboard.Key.Up => Math.Max(0, index - 1),
                _ => Math.Min(rows.Count - 1, index + 1)
            };
            OrbitraKeyboardNavigation.Focus(rows[index].Button);
            return true;
        }
        if (index >= 0 && args.Key is Keyboard.Key.Return or Keyboard.Key.Space)
        {
            if (!args.IsRepeat)
            {
                var id = rows[index].Id;
                popup.Close();
                OrbitraKeyboardNavigation.Focus(this);
                KeyboardSelected?.Invoke(new ItemSelectedEventArgs(id, this));
            }
            return true;
        }
        return false;
    }
}
