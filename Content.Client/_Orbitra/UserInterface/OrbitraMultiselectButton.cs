using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Styles the native multiselect popup while retaining native selection and modal behavior.</summary>
public sealed class OrbitraMultiselectButton<TKey> : MultiselectOptionButton<TKey>, IOrbitraKeyboardButton where TKey : notnull
{
    private Popup? _popup;

    public OrbitraMultiselectButton()
    {
        AddStyleClass("OrbitraLobbyButton");
        foreach (var child in Children)
            if (child is BoxContainer box)
                box.SeparationOverride = OrbitraUiMetrics.Small;
        CanKeyboardFocus = true;
        OrbitraMotion.AttachButton(this);
        OrbitraFocusRing.Attach(this);
    }

    public void KeyboardActivate(GUIBoundKeyEventArgs args)
    {
        if (args.State == BoundKeyState.Down) KeyBindDown(args);
        else KeyBindUp(args);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args) => Dispatch(args, true);
    protected override void KeyBindUp(GUIBoundKeyEventArgs args) => Dispatch(args, false);

    private void Dispatch(GUIBoundKeyEventArgs args, bool down)
    {
        if (Root == null)
            return;
        var modal = Root.ModalRoot;
        var opened = false;
        void Capture(Control child)
        {
            if (child is not Popup popup)
                return;
            _popup = popup;
            opened = true;
            OrbitraEditorStyles.Apply(popup);
            OrbitraMotion.BindPopup(popup, this);
            OrbitraKeyboardNavigation.Attach(popup);
        }
        // Подписка существует только внутри синхронного штатного открытия.
        modal.OnChildAdded += Capture;
        try
        {
            if (down) base.KeyBindDown(args);
            else base.KeyBindUp(args);
        }
        finally
        {
            modal.OnChildAdded -= Capture;
        }
        if (opened && _popup is { Visible: true })
            OrbitraMotion.Reveal(_popup, OrbitraMotion.MenuDuration);
    }
}
