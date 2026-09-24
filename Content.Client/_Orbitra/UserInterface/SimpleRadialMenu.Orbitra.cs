using System.Numerics;
using Content.Client._Orbitra.UserInterface;

namespace Content.Client.UserInterface.Controls;

public sealed partial class SimpleRadialMenu
{
    private bool _orbitraEmotes;

    public void EnableOrbitraEmotes()
    {
        if (_orbitraEmotes)
            return;
        _orbitraEmotes = true;
        AddStyleClass("OrbitraEmoteWheel");
        CloseButtonStyleClass = "OrbitraRadialClose";
        BackButtonStyleClass = "OrbitraRadialBack";
        ContextualButton.SetSize = new Vector2(24);
        OnOpen += RevealOrbitraEmotes;
        OnClose += FinishOrbitraEmotes;
    }

    private void StyleOrbitraEmoteLayers()
    {
        if (!_orbitraEmotes)
            return;
        foreach (var child in Children)
        {
            if (child is RadialContainer layer)
            {
                OrbitraMotion.AttachReveal(layer, OrbitraMotion.SectionDuration);
                foreach (var button in layer.Children)
                    OrbitraTooltips.Attach(button);
            }
        }
    }

    private void RevealOrbitraEmotes() => OrbitraMotion.Reveal(this, OrbitraMotion.MenuDuration);

    private void FinishOrbitraEmotes()
    {
        OrbitraTooltips.ClearOwned(this);
        OrbitraMotion.Finish(this);
        foreach (var child in Children)
            OrbitraMotion.Finish(child);
    }

    protected override void Dispose(bool disposing)
    {
        if (_orbitraEmotes)
        {
            OnOpen -= RevealOrbitraEmotes;
            OnClose -= FinishOrbitraEmotes;
            FinishOrbitraEmotes();
        }
        base.Dispose(disposing);
    }
}
