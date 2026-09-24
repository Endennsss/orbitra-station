using Content.Client._Orbitra.UserInterface;
using Robust.Client.UserInterface;

namespace Content.Client.Lobby.UI;

public sealed partial class CharacterPickerButton
{
    private void ApplyOrbitraPicker()
    {
        AddStyleClass("OrbitraCharacterCard");
        InternalHBox.Margin = new Thickness(8);
        DescriptionLabel.ToolTip = DescriptionLabel.Text;
        OrbitraTooltips.Attach(DescriptionLabel);
        OrbitraEditorStyles.Apply(this);
        OrbitraMotion.AttachButton(this);
    }
}
