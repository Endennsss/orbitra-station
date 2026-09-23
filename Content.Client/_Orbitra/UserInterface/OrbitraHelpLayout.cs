using System.Linq;
using Content.Client.Administration.UI.Bwoink;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Wraps actions in explicitly styled AHelp windows without replacing their controls.</summary>
internal static class OrbitraHelpLayout
{
    public static void Attach(BwoinkControl control)
    {
        if (control.HasStyleClass("OrbitraHelpLayout") || control.PopOut.Parent is not BoxContainer bar)
            return;
        control.AddStyleClass("OrbitraHelpLayout");
        var preferences = new WrapContainer { Name = "OrbitraHelpPreferences", SeparationOverride = 16, CrossSeparationOverride = 8 };
        var actions = new WrapContainer { Name = "OrbitraHelpActions", SeparationOverride = 8, CrossSeparationOverride = 8 };
        foreach (var child in bar.Children.ToArray())
        {
            child.Orphan();
            if (child.GetType() == typeof(Control))
            {
                child.Dispose();
                continue;
            }
            (child is CheckBox || child == control.PopOut ? preferences : actions).AddChild(child);
        }
        bar.SetHeight = float.NaN;
        bar.Orientation = BoxContainer.LayoutOrientation.Vertical;
        bar.SeparationOverride = OrbitraUiMetrics.Small;
        bar.Margin = new Thickness(0, OrbitraUiMetrics.Small, 0, 0);
        bar.AddChild(preferences);
        bar.AddChild(actions);

        // Одинаковая поверхность списка и переписки, без старой серой подложки.
        if (control.GetChild(0) is PanelContainer background)
        {
            background.RemoveStyleClass("BackgroundDark");
            background.AddStyleClass("OrbitraEditorSurface");
        }
        if (control.ChannelSelector.Parent is SplitContainer split)
        {
            split.SplitWidth = OrbitraUiMetrics.Medium;
            split.SetSplitFractionOnNextArrange(0.42f);
            if (split.Second is { } conversation)
            {
                conversation.Orphan();
                var panel = new PanelContainer { HorizontalExpand = true, VerticalExpand = true };
                panel.AddStyleClass("OrbitraHelpConversation");
                panel.AddChild(conversation);
                split.AddChild(panel);
            }
        }
    }
}
