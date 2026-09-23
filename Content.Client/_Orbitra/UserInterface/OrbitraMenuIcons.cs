using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Adds service icons without replacing labels, handlers or gameplay textures.</summary>
internal static class OrbitraMenuIcons
{
    public static void Apply(Control control)
    {
        if (control.Name is "DeleteButton" or "RemoveButton" or "SuicideButton" or "ServerShutdownButton" or
            "EndRound" or "RestartRound" or "RestartRoundNow" or "Kick" or "Ban")
            control.AddStyleClass(OrbitraButtonStyles.Danger);
        if (control is not Button button || control is CheckBox ||
            control.HasStyleClass("OrbitraServiceIcon"))
            return;
        var icon = control.Name switch
        {
            "ClearButton" => "list_x",
            "PreviousRecipeInHistoryButton" or "PreviousButton" => "chevron_left",
            "NextRecipeInHistoryButton" or "NextButton" => "chevron_right",
            "MenuGridViewButton" => "layout_grid",
            "FavoriteButton" => "star",
            "PopOut" => "external_link",
            "RefreshButton" or "RefreshListButton" => "refresh_cw",
            "DeleteButton" or "RemoveButton" => "trash",
            "CollapseButton" => "minus",
            _ => null
        };
        if (icon != null)
            Add(button, icon);
    }

    public static void Add(Button button, string icon)
    {
        if (button.HasStyleClass("OrbitraServiceIcon"))
            return;
        button.AddStyleClass("OrbitraServiceIcon");
        button.Label.Margin = new Thickness(OrbitraUiMetrics.IconSize + OrbitraUiMetrics.Small, 0, 0, 0);
        button.AddChild(new OrbitraIcon { Icon = icon, HorizontalAlignment = Control.HAlignment.Left });
    }
}
