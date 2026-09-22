using System.Numerics;
using Content.Client._Orbitra.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Non-measuring keyboard focus outline for explicitly styled controls.</summary>
internal sealed class OrbitraFocusRing : Control
{
    internal static void Attach(Control owner)
    {
        foreach (var child in owner.Children)
            if (child is OrbitraFocusRing)
                return;
        owner.AddChild(new OrbitraFocusRing());
    }
    public OrbitraFocusRing() => MouseFilter = MouseFilterMode.Ignore;

    protected override Vector2 MeasureOverride(Vector2 availableSize) => Vector2.Zero;

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (Parent == null)
            return;
        if (Parent.HasStyleClass("OrbitraTreeRow") && Parent.HasStyleClass("selected"))
            handle.DrawRect(new UIBox2(0, 2 * UIScale, 2 * UIScale, Math.Max(2 * UIScale, PixelHeight - 2 * UIScale)), OrbitraPalettes.Highlight.Text);
        if (Parent.HasKeyboardFocus() && Parent is not BaseButton { Disabled: true } && Parent is not Slider { Disabled: true })
            handle.DrawRect(new UIBox2(Vector2.One, Vector2.Max(Vector2.One, PixelSize - Vector2.One)), OrbitraPalettes.Highlight.Text, false);
    }
}
