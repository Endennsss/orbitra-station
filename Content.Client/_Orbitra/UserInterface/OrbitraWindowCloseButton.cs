using System.Numerics;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Компактная нейтральная иконка закрытия для окон меню.</summary>
internal sealed class OrbitraWindowCloseButton : OrbitraButton
{
    public OrbitraWindowCloseButton()
    {
        SetSize = new Vector2(OrbitraUiMetrics.CloseSize);
        AddStyleClass("OrbitraWindowClose");
        AddChild(new OrbitraIcon { Icon = "close" });
        ToolTip = Robust.Shared.Localization.Loc.GetString("orbitra-ui-close");
    }
}
