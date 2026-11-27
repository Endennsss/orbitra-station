using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controls;

#pragma warning disable IDE0130 // Расширение ванильного экрана подключения из папки Orbitra.
namespace Content.Client.Launcher;

public sealed partial class LauncherConnectingGui
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        ConnectionCard.SetWidth = Math.Clamp(availableSize.X - 2 * OrbitraUiMetrics.ScreenMargin, 0, 920);
        Docking.MaxHeight = Math.Clamp(availableSize.Y * 0.48f, 88, 338);
        return base.MeasureOverride(availableSize);
    }

    private void InitializeOrbitraPresentation()
    {
        BackgroundParallax.IsDockingBackground = true;
        BackgroundParallax.UseOrbitraSky = true;
        OrbitraKeyboardNavigation.Attach(this).HandleKey = ScrollPage;

        if (_state.Address is not { Length: > 0 } address)
            return;

        ConnectingAddress.Text = address;
        ConnectingAddress.ToolTip = address;
        ConnectingAddress.Visible = true;
    }

    private bool ScrollPage(KeyEventArgs args)
    {
        if (args.Control || args.Alt || args.System || args.Key is not (Keyboard.Key.PageUp or Keyboard.Key.PageDown))
            return false;

        var direction = args.Key == Keyboard.Key.PageUp ? -1 : 1;
        var reason = ConnectFail.Visible ? FailureScroll : DisconnectScroll;
        return reason.VisibleInTree && ScrollByPage(reason, direction) || ScrollByPage(PageScroll, direction);
    }

    private static bool ScrollByPage(ScrollContainer scroll, int direction)
    {
        var previous = scroll.GetScrollValue().Y;
        scroll.VScroll = previous + direction * scroll.Height * 0.85f;
        return scroll.GetScrollValue().Y != previous;
    }
}
