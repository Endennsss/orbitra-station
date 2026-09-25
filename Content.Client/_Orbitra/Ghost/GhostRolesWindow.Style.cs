using Content.Client._Orbitra.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Ghost.Controls.Roles;

public sealed partial class GhostRolesWindow
{
    private BoxContainer CreateOrbitraRoleCard(GhostRoleInfoBox info)
    {
        var card = new PanelContainer();
        card.AddStyleClass("OrbitraRoleCard");
        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        body.AddChild(info);
        card.AddChild(body);
        EntryContainer.AddChild(card);
        // Подключаем до добавления динамических кнопок, чтобы новые строки тоже получили стиль.
        OrbitraEditorStyles.Apply(card);
        return body;
    }
}
