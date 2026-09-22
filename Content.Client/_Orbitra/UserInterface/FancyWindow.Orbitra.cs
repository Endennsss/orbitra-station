using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.UserInterface; // Orbitra-Edit

namespace Content.Client.UserInterface.Controls;

public partial class FancyWindow
{
    /// <summary>Opts this window into entry-screen chrome without changing unrelated game windows.</summary>
    internal void ApplyOrbitraChrome()
    {
        if (HasStyleClass("OrbitraWindowChrome"))
            return;
        AddStyleClass("OrbitraWindowChrome");
        OnKeyBindDown += args =>
        {
            if (args.Function == Robust.Shared.Input.EngineKeyFunctions.UIClick && GetDragModeFor(args.RelativePosition) != DragMode.None)
                OrbitraMotion.Finish(this);
        };
        foreach (var child in Children)
        {
            if (child is PanelContainer panel)
            {
                panel.StyleClasses.Clear();
                panel.AddStyleClass("OrbitraWindowSurface");
            }
        }
        WindowHeader.StyleClasses.Clear();
        WindowHeader.AddStyleClass("OrbitraWindowHeader");
        if (WindowHeader.Parent is { } heading)
            heading.SetHeight = OrbitraUiMetrics.HeaderHeight;
        WindowTitle.RemoveStyleClass("LabelHeading");
        WindowTitle.AddStyleClass("OrbitraWindowTitle");
        if (WindowTitle.Parent is BoxContainer row)
        {
            row.Margin = new Thickness(OrbitraUiMetrics.WindowPadding, 6, OrbitraUiMetrics.Small, 6);
            row.SeparationOverride = OrbitraUiMetrics.Small;
            var close = new OrbitraWindowCloseButton();
            close.OnPressed += _ => OrbitraEntryWindow.RequestClose(this);
            row.AddChild(close);
            CloseButton.Visible = false;
        }
        ContentsContainer.RemoveStyleClass("WindowContentsContainer");
        ContentsContainer.Margin = new Thickness(OrbitraUiMetrics.WindowPadding);
        if (ContentsContainer.Parent is BoxContainer layout)
        {
            layout.SeparationOverride = 0;
            foreach (var child in layout.Children)
            {
                if (child is PanelContainer divider)
                    divider.Visible = false;
            }
        }
    }
}
