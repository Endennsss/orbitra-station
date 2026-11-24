using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.UserInterface; // Orbitra-Edit

namespace Content.Client._Orbitra.Lobby;

/// <summary>Кнопка лобби с общей анимацией цвета и учётом настроек доступности.</summary>
public sealed class OrbitraLobbyButton : OrbitraButton
{
    public OrbitraLobbyButton()
    {
        OrbitraMotion.AttachButton(this);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        ToolTip ??= Text;
        OrbitraTooltips.Attach(this);
    }
}
