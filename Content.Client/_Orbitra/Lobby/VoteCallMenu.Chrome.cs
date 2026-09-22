using Content.Client._Orbitra.Lobby;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Voting.UI;

public sealed partial class VoteCallMenu
{
    /// <summary>Styles the bespoke vote window only when opened through the entry flow.</summary>
    internal void ApplyOrbitraChrome()
    {
        if (CloseButton.Parent is not BoxContainer row || row.Parent is not BoxContainer layout)
            return;
        foreach (var child in Children)
        {
            if (child is PanelContainer panel)
            {
                panel.StyleClasses.Clear();
                panel.AddStyleClass("OrbitraWindowSurface");
            }
        }
        row.Orphan();
        var heading = new PanelContainer { SetHeight = 44, StyleClasses = { "OrbitraWindowHeader" }, Children = { row } };
        layout.AddChild(heading);
        heading.SetPositionInParent(0);
        row.Margin = new Thickness(16, 6, 8, 6);
        foreach (var child in row.Children)
        {
            if (child is Label title)
            {
                title.StyleClasses.Clear();
                title.AddStyleClass("FancyWindowTitle");
                title.AddStyleClass("OrbitraWindowTitle");
            }
        }
        CloseButton.Visible = false;
        var close = new OrbitraWindowCloseButton();
        close.OnPressed += _ => Close();
        row.AddChild(close);
        foreach (var child in layout.Children)
        {
            if (child is ScrollContainer scroll)
                scroll.Margin = new Thickness(16, 12, 16, 0);
            else if (child is PanelContainer && child != heading)
                child.Visible = false;
        }
        CreateButton.Margin = new Thickness(16, 12, 16, 8);
    }
}
