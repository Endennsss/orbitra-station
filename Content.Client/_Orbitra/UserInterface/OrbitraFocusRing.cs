using System.Numerics;
using Content.Client._Orbitra.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Non-measuring keyboard focus outline for explicitly styled controls.</summary>
internal sealed class OrbitraFocusRing : Control
{
    public OrbitraFocusRing() => MouseFilter = MouseFilterMode.Ignore;

    protected override Vector2 MeasureOverride(Vector2 availableSize) => Vector2.Zero;

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (Parent?.HasKeyboardFocus() == true)
            handle.DrawRect(new UIBox2(Vector2.One, Vector2.Max(Vector2.One, PixelSize - Vector2.One)), OrbitraPalettes.Highlight.Text, false);
    }
}
