using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Routes keyboard activation through the original button lifecycle.</summary>
public interface IOrbitraKeyboardButton
{
    void KeyboardActivate(GUIBoundKeyEventArgs args);
}

public class OrbitraButton : Button, IOrbitraKeyboardButton
{
    public OrbitraButton()
    {
        CanKeyboardFocus = true;
        OrbitraFocusRing.Attach(this);
    }
    public void KeyboardActivate(GUIBoundKeyEventArgs args)
    {
        if (args.State == BoundKeyState.Down) base.KeyBindDown(args);
        else base.KeyBindUp(args);
    }
}

public sealed class OrbitraCheckBox : CheckBox, IOrbitraKeyboardButton
{
    public OrbitraCheckBox()
    {
        CanKeyboardFocus = true;
        OrbitraFocusRing.Attach(this);
    }
    public void KeyboardActivate(GUIBoundKeyEventArgs args)
    {
        if (args.State == BoundKeyState.Down) base.KeyBindDown(args);
        else base.KeyBindUp(args);
    }
}

public class OrbitraContainerButton : ContainerButton, IOrbitraKeyboardButton
{
    public OrbitraContainerButton()
    {
        CanKeyboardFocus = true;
        OrbitraFocusRing.Attach(this);
    }
    public void KeyboardActivate(GUIBoundKeyEventArgs args)
    {
        if (args.State == BoundKeyState.Down) base.KeyBindDown(args);
        else base.KeyBindUp(args);
    }
}

public sealed class OrbitraTextureButton : TextureButton, IOrbitraKeyboardButton
{
    public OrbitraTextureButton()
    {
        CanKeyboardFocus = true;
        OrbitraFocusRing.Attach(this);
    }

    public void KeyboardActivate(GUIBoundKeyEventArgs args)
    {
        if (args.State == BoundKeyState.Down) base.KeyBindDown(args);
        else base.KeyBindUp(args);
    }
}
