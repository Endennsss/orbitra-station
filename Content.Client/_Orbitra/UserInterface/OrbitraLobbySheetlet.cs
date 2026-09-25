using Content.Client._Orbitra.Stylesheets;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Robust.Client.Graphics;
using Content.Client.Resources;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Local lobby styles; does not replace in-round controls or global palettes.</summary>
[CommonSheetlet]
public sealed class OrbitraLobbySheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var panel = Box(OrbitraPalettes.PanelInset.WithAlpha(0.96f), OrbitraUiMetrics.WindowPadding);
        var normal = Box(OrbitraPalettes.PanelBackground, OrbitraUiMetrics.Medium);
        var hover = Box(OrbitraPalettes.PanelHighlight, OrbitraUiMetrics.Medium);
        var pressed = Box(OrbitraPalettes.PanelBorder, OrbitraUiMetrics.Medium);
        var primary = Box(OrbitraPalettes.Primary.Element, OrbitraUiMetrics.Large);
        var option = new StyleBoxFlat(OrbitraPalettes.PanelBackground);
        option.SetContentMarginOverride(StyleBox.Margin.Horizontal, OrbitraUiMetrics.Medium);
        option.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
        var optionHover = new StyleBoxFlat(option) { BackgroundColor = OrbitraPalettes.PanelHighlight };
        var journalTab = new StyleBoxFlat(Color.Transparent);
        journalTab.SetContentMarginOverride(StyleBox.Margin.Horizontal, OrbitraUiMetrics.Medium);
        var journalSelected = new StyleBoxFlat(journalTab) { BorderColor = OrbitraPalettes.Highlight.Text, BorderThickness = new Thickness(0, 0, 0, 2) };
        var rules = new List<StyleRule>
        {
            E<Content.Client.UserInterface.Controls.FancyTree.FancyTree>().Class("OrbitraEditorControl")
                .Prop(Content.Client.UserInterface.Controls.FancyTree.FancyTree.StylePropertyLineColor, OrbitraPalettes.IconPressed)
                .Prop(Content.Client.UserInterface.Controls.FancyTree.FancyTree.StylePropertyLineWidth, 1),
            E<PanelContainer>().Class("OrbitraHelpConversation").Panel(Box(OrbitraPalettes.PanelInset, OrbitraUiMetrics.Small)),
            E<Button>().Class("OrbitraGroupHeader").ParentOf(E<Label>()).Prop(Label.StylePropertyAlignMode, Label.AlignMode.Left),
            E<Content.Client.UserInterface.Controls.StripeBack>().Class("OrbitraEditorControl")
                .Prop(Content.Client.UserInterface.Controls.StripeBack.StylePropertyBackground, new StyleBoxFlat(Color.Transparent)),
            E<ContainerButton>().Class("OrbitraTreeRow", ContainerButton.StyleClassButton).Box(new StyleBoxFlat(Color.Transparent)).Modulate(Color.White),
            E<ContainerButton>().Class("OrbitraTreeRow", ContainerButton.StyleClassButton).PseudoNormal().Box(new StyleBoxFlat(Color.Transparent)).Modulate(Color.White),
            E<ContainerButton>().Class("OrbitraTreeRow", ContainerButton.StyleClassButton).PseudoHovered().Box(new StyleBoxFlat(OrbitraPalettes.PanelHighlight)).Modulate(Color.White),
            E<ContainerButton>().Class("OrbitraTreeRow", ContainerButton.StyleClassButton).PseudoPressed().Box(new StyleBoxFlat(OrbitraPalettes.PanelHighlight)).Modulate(Color.White),
            E<ContainerButton>().Class("OrbitraTreeRow", ContainerButton.StyleClassButton, "selected").Box(new StyleBoxFlat(OrbitraPalettes.PanelHighlight)).Modulate(Color.White),
            E<PanelContainer>().Class("OrbitraEditorControl", "even-row").Panel(new StyleBoxFlat(Color.Transparent)),
            E<PanelContainer>().Class("OrbitraEditorControl", "odd-row").Panel(new StyleBoxFlat(Color.Transparent)),
            E<ContainerButton>().Class("OrbitraJournalTab", ContainerButton.StyleClassButton).Box(journalTab),
            E<ContainerButton>().Class("OrbitraJournalTab", ContainerButton.StyleClassButton).PseudoNormal().Box(journalTab),
            E<ContainerButton>().Class("OrbitraJournalTab", ContainerButton.StyleClassButton).PseudoHovered().Box(new StyleBoxFlat(journalTab) { BackgroundColor = OrbitraPalettes.PanelHighlight }),
            E<ContainerButton>().Class("OrbitraJournalTab", ContainerButton.StyleClassButton).PseudoPressed().Box(journalSelected),
            E<ContainerButton>().Class("OrbitraJournalTab").ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(13)),
            E<Label>().Class("OrbitraJournalDate").Font(sheet.BaseFont.GetFont(16, FontKind.Bold)),
            E<PanelContainer>().Class("OrbitraWindowSurface").Panel(new StyleBoxFlat(OrbitraPalettes.PanelInset) { BorderColor = OrbitraPalettes.PanelBorder, BorderThickness = new Thickness(1) }),
            E<PanelContainer>().Class("OrbitraWindowHeader").Panel(new StyleBoxFlat(OrbitraPalettes.PanelBackground)),
            E<Label>().Class("OrbitraWindowTitle", "FancyWindowTitle").Font(sheet.BaseFont.GetFont(14, FontKind.Bold)),
            E<ContainerButton>().Class("OrbitraWindowClose", ContainerButton.StyleClassButton).Box(new StyleBoxFlat(Color.Transparent)),
            E<ContainerButton>().Class("OrbitraWindowClose", ContainerButton.StyleClassButton).PseudoNormal().Box(new StyleBoxFlat(Color.Transparent)).Modulate(Color.White),
            E<ContainerButton>().Class("OrbitraWindowClose", ContainerButton.StyleClassButton).PseudoHovered().Box(new StyleBoxFlat(OrbitraPalettes.PanelHighlight)),
            E<PanelContainer>().Class("OrbitraCrewCard").Panel(new StyleBoxFlat(Color.Transparent)),
            E<PanelContainer>().Class("OrbitraRoundPanel").Panel(Box(OrbitraPalettes.PanelInset.WithAlpha(0.96f), 8)),
            E<ContainerButton>().Class(ContainerButton.StyleClassButton, "OrbitraOptionRow").Box(option).MinHeight(32).Modulate(Color.White),
            E<ContainerButton>().Class(ContainerButton.StyleClassButton, "OrbitraOptionRow").PseudoNormal().Box(option),
            E<ContainerButton>().Class(ContainerButton.StyleClassButton, "OrbitraOptionRow").PseudoHovered().Box(optionHover),
            E<ContainerButton>().Class(ContainerButton.StyleClassButton, "OrbitraOptionRow").PseudoPressed().Box(optionHover),
            E<ContainerButton>().Class("OrbitraOptionRow").ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(13))
                .FontColor(OrbitraPalettes.Primary.Text),
            E<TextureRect>().Class("OrbitraBrandLogo").Prop(TextureRect.StylePropertyTexture,
                ResCache.GetTexture("/Textures/_Orbitra/Interface/Brand/logo.png")),
            E<PanelContainer>().Class("OrbitraLobbyPanel").Panel(panel),
            E<PanelContainer>().Class("OrbitraEditorSurface").Panel(new StyleBoxFlat(OrbitraPalettes.PanelInset.WithAlpha(0.99f))),
            E<PanelContainer>().Class("OrbitraLobbyScrim").Panel(new StyleBoxFlat(Color.Black.WithAlpha(0.70f))),
            E<LineEdit>().Class("OrbitraEditorControl").Prop(LineEdit.StylePropertyStyleBox, Box(OrbitraPalettes.PanelInset, 8)),
            E<ItemList>().Class("OrbitraEditorControl")
                .Prop(ItemList.StylePropertyBackground, new StyleBoxFlat(OrbitraPalettes.PanelInset))
                .Prop(ItemList.StylePropertyItemBackground, new StyleBoxFlat(Color.Transparent))
                .Prop(ItemList.StylePropertySelectedItemBackground, new StyleBoxFlat(OrbitraPalettes.PanelHighlight))
                .Prop(ItemList.StylePropertyDisabledItemBackground, new StyleBoxFlat(OrbitraPalettes.PanelInset)),
            E<Slider>().Class("OrbitraEditorControl")
                .Prop(Slider.StylePropertyBackground, Box(OrbitraPalettes.PanelInset, 4))
                .Prop(Slider.StylePropertyForeground, Box(Color.Transparent, 4))
                .Prop(Slider.StylePropertyFill, Box(OrbitraPalettes.PanelBorder, 4))
                .Prop(Slider.StylePropertyGrabber, Box(OrbitraPalettes.IconNormal, 5)),
            E<TabContainer>().Class("OrbitraEditorControl")
                .Prop(TabContainer.StylePropertyTabStyleBox, pressed)
                .Prop(TabContainer.StylePropertyTabStyleBoxInactive, normal)
                .Prop(TabContainer.StylePropertyPanelStyleBox, Box(OrbitraPalettes.PanelInset, 4)),
            E<Label>().Class("OrbitraLobbyHeading").Font(sheet.BaseFont.GetFont(14, FontKind.Bold))
                .FontColor(OrbitraPalettes.Primary.Text),
            E<Label>().Class("OrbitraEditorControl", "FancyWindowTitle").Font(sheet.BaseFont.GetFont(14, FontKind.Bold)),
            E<Label>().Class("OrbitraLobbyTitle").Font(sheet.BaseFont.GetFont(22, FontKind.Bold))
                .FontColor(OrbitraPalettes.Highlight.Text),
            E<Label>().Class("OrbitraLobbyMuted").Font(sheet.BaseFont.GetFont(12)).FontColor(OrbitraPalettes.IconNormal),
            E<RichTextLabel>().Class("OrbitraLobbyMuted").Font(sheet.BaseFont.GetFont(11)).FontColor(OrbitraPalettes.IconNormal),
        };
        foreach (var style in new[] { "OrbitraLobbyButton", "OrbitraLobbyPrimary", "OrbitraDangerButton", "OrbitraNavigationButton",
                     OrbitraButtonStyles.Primary, OrbitraButtonStyles.Secondary, OrbitraButtonStyles.Ghost, OrbitraButtonStyles.Danger })
        {
            var isPrimary = style is "OrbitraLobbyPrimary" or OrbitraButtonStyles.Primary;
            var isGhost = style == OrbitraButtonStyles.Ghost;
            var background = isGhost ? new StyleBoxFlat(normal) { BackgroundColor = Color.Transparent, BorderColor = Color.Transparent } : isPrimary ? primary : normal;
            rules.AddRange([
                E<ContainerButton>().Class(style, ContainerButton.StyleClassButton).Box(background).MinHeight(isPrimary ? OrbitraUiMetrics.ActionHeight : OrbitraUiMetrics.ElementHeight).Modulate(Color.White),
                E<ContainerButton>().Class(style, ContainerButton.StyleClassButton).PseudoNormal().Box(background).Modulate(Color.White),
                E<ContainerButton>().Class(style, ContainerButton.StyleClassButton).PseudoHovered().Box(hover).Modulate(Color.White),
                E<ContainerButton>().Class(style, ContainerButton.StyleClassButton).PseudoPressed().Box(pressed).Modulate(Color.White),
                E<ContainerButton>().Class(style, ContainerButton.StyleClassButton).PseudoDisabled().Box(background).Modulate(Color.White.WithAlpha(0.65f)),
                E<ContainerButton>().Class(style, ContainerButton.StyleClassButton).ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(13))
                    .FontColor(OrbitraPalettes.Highlight.Text),
            ]);
        }
        var selected = Box(OrbitraPalettes.PanelHighlight, 12);
        // Служебные строки не наследуют цветовую модуляцию текстурных кнопок.
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
        {
            rules.Add(E<ContainerButton>().Class(ContainerButton.StyleClassButton, "OrbitraOptionRow")
                .Pseudo(state).Modulate(state == "disabled" ? Color.White.WithAlpha(0.65f) : Color.White));
            rules.Add(E<ContainerButton>().Class(ContainerButton.StyleClassButton, "OrbitraOptionRow", "OrbitraTableRow")
                .Pseudo(state).Box(new StyleBoxFlat(state is "hover" or "pressed" ? OrbitraPalettes.PanelHighlight : Color.Transparent)));
        }
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
            rules.Add(E<Content.Client.UserInterface.Controls.ConfirmButton>().Class("OrbitraEditorControl")
                .Pseudo("confirm-" + state).Box(state == "pressed" ? pressed : hover)
                .Modulate(state == "disabled" ? Color.White.WithAlpha(0.65f) : Color.White));
        selected.BorderThickness = new Thickness(3, 0, 0, 0);
        selected.BorderColor = OrbitraPalettes.Highlight.Element;
        rules.Add(E<ContainerButton>().Class("OrbitraNavigationButton").PseudoPressed().Box(selected));
        rules.Add(E<ContainerButton>().Class("OrbitraDangerButton").ParentOf(E<Label>()).FontColor(sheet.NegativePalette.Text));
        rules.Add(E<ContainerButton>().Class(OrbitraButtonStyles.Danger).ParentOf(E<Label>()).FontColor(sheet.NegativePalette.Text));
        foreach (var icon in new[] { "close", "check", "chevron_down", "chevron_right", "eye", "eye_star", "shuffle", "info", "warning",
                     "search", "list_filter", "list_x", "chevron_left", "chevron_up", "minus", "list", "layout_grid", "star",
                     "external_link", "refresh_cw", "trash", "pin", "pin_off", "circle_question_mark",
                     "menu", "user_round", "drama", "hammer", "hand", "gavel", "shovel", "bug", "navigation", "zap", "wand_sparkles" })
            rules.Add(E<TextureRect>().Class("OrbitraIcon-" + icon).Prop(TextureRect.StylePropertyTexture,
                sheet.GetTextureOr(new Robust.Shared.Utility.ResPath("_Orbitra/Interface/Icons/" + icon + ".svg.192dpi.png"), new Robust.Shared.Utility.ResPath("/Textures"))));
        foreach (var (style, icon) in new[] { ("OrbitraCheckIcon", "checkbox"), ("OrbitraCheckIconChecked", "checkbox_checked") })
            rules.Add(E<TextureRect>().Class(style).Prop(TextureRect.StylePropertyTexture,
                sheet.GetTextureOr(new Robust.Shared.Utility.ResPath("_Orbitra/Interface/Icons/" + icon + ".svg.192dpi.png"), new Robust.Shared.Utility.ResPath("/Textures"))));
        rules.Add(E<TextureRect>().Class("OrbitraCheckIcon", CheckBox.StyleClassCheckBoxChecked)
            .Prop(TextureRect.StylePropertyTexture, sheet.GetTextureOr(new Robust.Shared.Utility.ResPath("_Orbitra/Interface/Icons/checkbox_checked.svg.192dpi.png"), new Robust.Shared.Utility.ResPath("/Textures"))));
        var compact = Box(OrbitraPalettes.PanelBackground, 4);
        compact.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
        {
            var background = state == "hover" ? OrbitraPalettes.PanelHighlight : state == "pressed" ? OrbitraPalettes.PanelBorder : OrbitraPalettes.PanelBackground;
            rules.Add(E<ContainerButton>().Class("OrbitraIconButton", ContainerButton.StyleClassButton)
                .Pseudo(state).Box(new StyleBoxFlat(compact) { BackgroundColor = background }).MinHeight(32));
            rules.Add(E<ContainerButton>().Class("OrbitraCompactRow", ContainerButton.StyleClassButton)
                .Pseudo(state).Box(new StyleBoxFlat(compact) { BackgroundColor = background }).MinHeight(32));
        }
        rules.Add(E<ContainerButton>().Class("OrbitraCompactRow", ContainerButton.StyleClassButton).Box(compact).MinHeight(32));
        var tooltip = Box(OrbitraPalettes.PanelInset, OrbitraUiMetrics.Medium);
        tooltip.SetContentMarginOverride(StyleBox.Margin.All, OrbitraUiMetrics.Medium);
        rules.Add(E<PanelContainer>().Class("OrbitraTooltip").Panel(tooltip));
        rules.Add(E<Robust.Client.UserInterface.CustomControls.Tooltip>().Class("OrbitraTooltipContent")
            .Panel(new StyleBoxFlat(Color.Transparent)));
        rules.Add(E<PanelContainer>().Class("OrbitraNotification").Panel(Box(OrbitraPalettes.PanelBackground, OrbitraUiMetrics.Small)));
        foreach (var (kind, color) in new[] { ("Info", OrbitraPalettes.IconNormal), ("Success", sheet.PositivePalette.Text), ("Warning", sheet.HighlightPalette.Text), ("Error", sheet.NegativePalette.Text) })
            rules.Add(E<PanelContainer>().Class("OrbitraNotification", "OrbitraNotification" + kind)
                .Panel(new StyleBoxFlat(Box(OrbitraPalettes.PanelBackground, OrbitraUiMetrics.Small)) { BorderColor = color, BorderThickness = new Thickness(2, 0, 0, 0) }));
        foreach (var vertical in new[] { true, false })
        {
            var grabber = new StyleBoxFlat(OrbitraPalettes.IconNormal)
            {
                BorderColor = Color.Transparent,
                BorderThickness = vertical ? new Thickness(3, 2, 3, 2) : new Thickness(2, 3, 2, 3),
            };
            // Узкая видимая ручка внутри прежней области захвата в 12 единиц.
            grabber.SetContentMarginOverride(StyleBox.Margin.Horizontal, vertical ? 6 : 12);
            grabber.SetContentMarginOverride(StyleBox.Margin.Vertical, vertical ? 12 : 6);
            var selector = vertical ? E<VScrollBar>() : E<HScrollBar>();
            rules.Add(selector.Class("OrbitraEditorControl").Prop(ScrollBar.StylePropertyTrack, new StyleBoxFlat(OrbitraPalettes.PanelBackground))
                .Prop(ScrollBar.StylePropertyGrabber, grabber));
            rules.Add((vertical ? E<VScrollBar>() : E<HScrollBar>()).Class("OrbitraEditorControl").Pseudo(ScrollBar.StylePseudoClassHover)
                .Prop(ScrollBar.StylePropertyGrabber, new StyleBoxFlat(grabber) { BackgroundColor = OrbitraPalettes.Primary.Text }));
            rules.Add((vertical ? E<VScrollBar>() : E<HScrollBar>()).Class("OrbitraEditorControl").Pseudo(ScrollBar.StylePseudoClassGrabbed)
                .Prop(ScrollBar.StylePropertyGrabber, new StyleBoxFlat(grabber) { BackgroundColor = OrbitraPalettes.Highlight.Text }));
        }
        return rules.ToArray();
    }

    private static StyleBoxFlat Box(Color color, int padding)
    {
        var box = new StyleBoxFlat(color)
        {
            BorderColor = OrbitraPalettes.PanelBorder.WithAlpha(0.65f),
            BorderThickness = new Thickness(1),
        };
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, padding);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, 6);
        return box;
    }
}
