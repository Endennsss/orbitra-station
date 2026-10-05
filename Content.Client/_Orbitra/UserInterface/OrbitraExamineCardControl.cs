using System.Numerics;
using Content.Client._Orbitra.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Карточка осмотра предмета, добавляемая в историю чата.</summary>
public sealed class OrbitraExamineCardControl : PanelContainer
{
    public OrbitraExamineCardControl(EntityUid target, string title, FormattedMessage description)
    {
        HorizontalExpand = false;
        HorizontalAlignment = HAlignment.Left;
        MaxWidth = 560;
        Margin = new Thickness(OrbitraUiMetrics.Medium, 0, OrbitraUiMetrics.Small, 0);
        PanelOverride = new StyleBoxFlat(OrbitraPalettes.PanelInset)
        {
            BorderColor = OrbitraPalettes.PanelBorder,
            BorderThickness = new Thickness(1),
        };

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = OrbitraUiMetrics.Small,
            Margin = new Thickness(OrbitraUiMetrics.Medium),
        };
        var heading = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = OrbitraUiMetrics.Small,
        };
        var sprite = new SpriteView { SetSize = new Vector2(48, 48), VerticalAlignment = VAlignment.Center };
        sprite.SetEntity(target);
        heading.AddChild(sprite);
        heading.AddChild(new Label
        {
            Text = title,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            StyleClasses = { "OrbitraAntagBriefingTitle" },
        });
        var body = new RichTextLabel
        {
            HorizontalExpand = true,
            StyleClasses = { OrbitraChatSheetlet.ChatText },
        };
        body.SetMessage(description, tagsAllowed: null, defaultColor: OrbitraPalettes.Primary.Text);
        content.AddChild(heading);
        content.AddChild(body);
        AddChild(content);
    }
}
