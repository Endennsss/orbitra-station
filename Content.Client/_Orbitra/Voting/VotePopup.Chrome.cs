using Robust.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Content.Client._Orbitra.Stylesheets;
using Content.Client._Orbitra.UserInterface;
using Robust.Shared.Maths;

namespace Content.Client.Voting.UI;

public sealed partial class VotePopup
{
    /// <summary>Адаптирует компактный попап голосования к узкой панели лобби.</summary>
    private void ApplyOrbitraChrome()
    {
        // Orbitra-Edit - варианты голосования идут в один адаптивный столбец,
        // чтобы длинные названия режима не выезжали за границы панели.
        HorizontalExpand = true;
        MinWidth = 0;
        MaxWidth = 460;
        if (GetChild(0) is PanelContainer panel)
            panel.PanelOverride = new StyleBoxFlat(OrbitraPalettes.PanelBackground)
        {
            BorderColor = OrbitraPalettes.PanelBorder,
            BorderThickness = new Thickness(1),
        };
        VoteCaller.HorizontalExpand = true;
        VoteCaller.ClipText = true;
        VoteCaller.ToolTip = VoteCaller.Text;
        VoteTitle.HorizontalExpand = true;
        VoteTitle.ToolTip = VoteTitle.Text;
        VoteOptionsContainer.Columns = 1;

        foreach (var child in VoteOptionsContainer.Children)
        {
            if (child is not Button button)
                continue;

            button.HorizontalExpand = true;
            button.MinWidth = 0;
            button.ClipText = true;
            button.ToolTip = button.Text;
            button.AddStyleClass(OrbitraButtonStyles.Secondary);
        }
    }
}
