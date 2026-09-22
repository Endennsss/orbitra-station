using Robust.Client.UserInterface;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Reversible visibility for existing panels without replacing or measuring their contents.</summary>
internal sealed class OrbitraVisibility(Control target)
{
    private readonly OrbitraInputBlock _input = new();
    private bool _shown = target.Visible;

    internal void SetShown(bool shown, bool immediate = false)
    {
        if (_shown == shown && !immediate)
            return;
        _shown = shown;
        _input.Restore();
        if (immediate)
        {
            OrbitraMotion.Finish(target);
            target.Visible = shown;
            return;
        }
        if (shown)
        {
            target.Visible = true;
            OrbitraMotion.Reveal(target, OrbitraMotion.MenuDuration);
        }
        else if (target.Visible)
        {
            _input.Block(target);
            OrbitraMotion.Hide(target, OrbitraMotion.MenuCloseDuration, () =>
            {
                if (!_shown && !target.Disposed)
                    target.Visible = false;
                _input.Restore();
            });
        }
    }
}
