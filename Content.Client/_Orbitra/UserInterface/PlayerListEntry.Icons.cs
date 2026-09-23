using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Robust.Client.UserInterface;

namespace Content.Client.Administration.UI.CustomControls;

public sealed partial class PlayerListEntry
{
    private bool _orbitraPinned;
    private OrbitraIcon? _orbitraPin;

    /// <summary>Changes only the pin presentation in an explicitly styled player list.</summary>
    internal bool ApplyOrbitraPin()
    {
        if (!HasStyleClass("OrbitraEditorControl"))
            return false;
        // Полная подпись доступна даже при обрезании имени рядом с кнопкой закрепления.
        if (Parent != null)
            Parent.ToolTip = PlayerEntryLabel.Text;
        if (_orbitraPin == null)
        {
            _orbitraPin = new OrbitraIcon();
            PlayerEntryPinButton.TexturePath = null!;
            PlayerEntryPinButton.TextureNormal = null;
            PlayerEntryPinButton.SetSize = new Vector2(32);
            PlayerEntryPinButton.CanKeyboardFocus = true;
            PlayerEntryPinButton.AddChild(_orbitraPin);
            OrbitraFocusRing.Attach(PlayerEntryPinButton);
            OrbitraTooltips.Attach(PlayerEntryPinButton);
        }
        _orbitraPin.Icon = _orbitraPinned ? "pin" : "pin_off";
        PlayerEntryPinButton.ToolTip = Loc.GetString(_orbitraPinned ? "orbitra-ui-unpin" : "orbitra-ui-pin");
        return true;
    }
}
