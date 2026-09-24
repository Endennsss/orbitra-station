using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Examine;

public sealed partial class ExamineSystem
{
    private void ConfigureOrbitraExamine()
    {
        if (_examineTooltipOpen is not { } popup || popup.GetChild(0) is not PanelContainer panel ||
            panel.GetChild(0) is not BoxContainer content)
            return;

        var screen = _userInterfaceManager.RootControl.Size;
        popup.MaxWidth = Math.Min(400, Math.Max(1, screen.X - OrbitraUiMetrics.ScreenMargin * 2));
        panel.RemoveStyleClass(StyleClassEntityTooltip);
        panel.AddStyleClass("OrbitraTooltip");
        panel.ModulateSelfOverride = Color.White;
        content.MaxWidth = Math.Max(1, popup.MaxWidth - OrbitraUiMetrics.Medium * 2);
        content.SeparationOverride = OrbitraUiMetrics.Small;
        if (content.GetChild(0) is BoxContainer heading)
        {
            heading.Margin = new Thickness(0);
            foreach (var child in heading.Children)
                if (child is RichTextLabel label)
                    label.HorizontalExpand = true;
        }
        StyleOrbitraExamineChildren(content);
        panel.Measure(new Vector2(popup.MaxWidth, float.PositiveInfinity));
        if (popup.Visible)
        {
            var size = Vector2.Max(new Vector2(Math.Min(300, popup.MaxWidth), 0), panel.DesiredSize);
            var margin = new Vector2(OrbitraUiMetrics.ScreenMargin);
            var position = Vector2.Clamp(_popupPos.Position, margin, Vector2.Max(margin, screen - size - margin));
            popup.Open(UIBox2.FromDimensions(position, size));
        }
    }

    private static void StyleOrbitraExamineChildren(Control root)
    {
        foreach (var child in root.Children)
        {
            if (child is ExamineButton button)
            {
                button.RemoveStyleClass(ExamineButton.StyleClassExamineButton);
                button.AddStyleClass(ContainerButton.StyleClassButton);
                button.AddStyleClass(OrbitraButtonStyles.Secondary);
                button.AddStyleClass("OrbitraIconButton");
                button.Icon.SetSize = new Vector2(24);
                button.CanKeyboardFocus = true;
                OrbitraTooltips.Attach(button);
            }
            else
                StyleOrbitraExamineChildren(child);
        }
    }
}
