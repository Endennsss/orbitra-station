using System.Numerics;
using Content.Client._Orbitra.UserInterface;

#pragma warning disable IDE0130 // Расширение ванильного экрана подключения из папки Orbitra.
namespace Content.Client.Launcher;

public sealed partial class LauncherConnectingGui
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        ConnectionCard.SetWidth = Math.Clamp(availableSize.X - 2 * OrbitraUiMetrics.ScreenMargin, 0, 520);
        return base.MeasureOverride(availableSize);
    }

    private void InitializeOrbitraPresentation()
    {
        OrbitraMenuBackground.Attach(this, BackgroundParallax);
        OrbitraKeyboardNavigation.Attach(this);

        if (_state.Address is not { Length: > 0 } address)
            return;

        ConnectingAddress.Text = address;
        ConnectingAddress.ToolTip = address;
        ConnectingAddress.Visible = true;
    }
}
