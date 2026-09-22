using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Compact neutral close icon for entry-flow windows.</summary>
internal sealed class OrbitraWindowCloseButton : Button
{
    public OrbitraWindowCloseButton()
    {
        SetSize = new Vector2(OrbitraUiMetrics.CloseSize);
        AddStyleClass("OrbitraWindowClose");
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var p = PixelSize / 2;
        var d = new Vector2(5 * UIScale);
        handle.DrawLine(p - d, p + d, Color.LightGray);
        handle.DrawLine(p + new Vector2(-d.X, d.Y), p + new Vector2(d.X, -d.Y), Color.LightGray);
    }
}
