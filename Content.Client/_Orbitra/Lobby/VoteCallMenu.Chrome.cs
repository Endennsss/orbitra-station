using System.Numerics;
using Content.Client._Orbitra.Lobby;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.UserInterface; // Orbitra-Edit

namespace Content.Client.Voting.UI;

public sealed partial class VoteCallMenu
{
    /// <summary>Styles the bespoke vote window only when opened through the entry flow.</summary>
    internal void ApplyOrbitraChrome()
    {
        // Orbitra-Edit - шапка и подпись добавляются поверх штатной разметки, поэтому
        // минимальный размер из XAML (350x200) оставляет скроллбар и обрезает подпись.
        MinSize = new Vector2(440, 280);
        SetSize = Vector2.Max(Size, MinSize);

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
        var heading = new PanelContainer { SetHeight = OrbitraUiMetrics.HeaderHeight, StyleClasses = { "OrbitraWindowHeader" }, Children = { row } };
        layout.AddChild(heading);
        heading.SetPositionInParent(0);
        row.Margin = new Thickness(16, 2, 8, 2);
        foreach (var child in row.Children)
        {
            if (child is Label title)
            {
                title.StyleClasses.Clear();
                title.AddStyleClass("FancyWindowTitle");
                title.AddStyleClass("OrbitraWindowTitle");
                title.ClipText = true;
                title.HorizontalExpand = true;
                title.TooltipSupplier = _ => new OrbitraTooltip(title.Text ?? "");
            }
        }
        CloseButton.Visible = false;
        var close = new OrbitraWindowCloseButton();
        close.OnPressed += _ => OrbitraEntryWindow.RequestClose(this);
        row.AddChild(close);
        foreach (var child in layout.Children)
        {
            if (child is ScrollContainer scroll)
                scroll.Margin = new Thickness(16, 12, 16, 0);
            else if (child is PanelContainer && child != heading)
                child.Visible = false;
            else if (child is Label footer)
            {
                footer.Margin = new Thickness(16, 0, 16, 8);
                footer.MinHeight = 20;
                footer.VerticalAlignment = VAlignment.Center;
                footer.HorizontalExpand = true;
                footer.ClipText = true;
                footer.ToolTip = footer.Text;
            }
        }
        CreateButton.Margin = new Thickness(16, 12, 16, 8);
    }
}
