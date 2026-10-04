using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Content.Client._Orbitra.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Рендерит приветствие антагониста отдельной карточкой в чате.</summary>
public sealed class OrbitraBriefingTag : IMarkupTagHandler
{
    public string Name => "orbitra-briefing";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        if (!node.Attributes.TryGetValue("payload", out var payload) || !payload.TryGetString(out var encoded))
        {
            control = null;
            return false;
        }

        var markup = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        if (!FormattedMessage.TryFromMarkup(markup, out var message))
        {
            control = null;
            return false;
        }

        var panel = new PanelContainer
        {
            MinWidth = 320,
            MaxWidth = 620,
            HorizontalExpand = true,
            StyleClasses = { "OrbitraAntagBriefing" }
        };
        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = OrbitraUiMetrics.Small,
            Margin = new Thickness(OrbitraUiMetrics.Medium)
        };
        var accent = new PanelContainer
        {
            MinHeight = 3,
            HorizontalExpand = true,
            StyleClasses = { "OrbitraAntagBriefingAccent" }
        };
        var title = new Label
        {
            Text = Loc.GetString("orbitra-antag-briefing-title"),
            StyleClasses = { "OrbitraAntagBriefingTitle" }
        };
        var body = new RichTextLabel { HorizontalExpand = true, StyleClasses = { "OrbitraAntagBriefingText" } };
        body.SetMessage(message);
        column.AddChild(accent);
        column.AddChild(title);
        column.AddChild(body);
        panel.AddChild(column);
        control = panel;
        return true;
    }
}
