using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.VoiceChat;

/// <summary>
/// Draws voice cards in the popup layer, above the gameplay HUD controls.
/// </summary>
internal sealed class OrbitraVoiceSpeakerCardControl : Control
{
    private readonly OrbitraVoiceSpeakerOverlay _overlay;

    public OrbitraVoiceSpeakerCardControl(OrbitraVoiceSpeakerOverlay overlay)
    {
        _overlay = overlay;
        HorizontalExpand = true;
        VerticalExpand = true;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        _overlay.DrawScreen(handle, new Vector2(PixelWidth, PixelHeight), UIScale);
    }
}
