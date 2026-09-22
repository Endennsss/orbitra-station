using Content.Client._Orbitra.Stylesheets;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Robust.Client.Graphics;
using Content.Client.Resources;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Orbitra.Lobby;

/// <summary>Local lobby styles; does not replace in-round controls or global palettes.</summary>
[CommonSheetlet]
public sealed class OrbitraLobbySheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var panel = Box(OrbitraPalettes.PanelInset.WithAlpha(0.96f), 16);
        var normal = Box(OrbitraPalettes.PanelBackground, 12);
        var hover = Box(OrbitraPalettes.PanelHighlight, 12);
        var pressed = Box(OrbitraPalettes.PanelBorder, 12);
        var primary = Box(OrbitraPalettes.Primary.Element, 16);
        var option = new StyleBoxFlat(OrbitraPalettes.PanelBackground);
        option.SetContentMarginOverride(StyleBox.Margin.Horizontal, 12);
        option.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
        var optionHover = new StyleBoxFlat(option) { BackgroundColor = OrbitraPalettes.PanelHighlight };
        var journalTab = new StyleBoxFlat(Color.Transparent);
        journalTab.SetContentMarginOverride(StyleBox.Margin.Horizontal, 12);
        var journalSelected = new StyleBoxFlat(journalTab) { BorderColor = OrbitraPalettes.Highlight.Text, BorderThickness = new Thickness(0, 0, 0, 2) };
        var rules = new List<StyleRule>
        {
            E<Content.Client.UserInterface.Controls.StripeBack>().Class("OrbitraEditorControl")
                .Prop(Content.Client.UserInterface.Controls.StripeBack.StylePropertyBackground, new StyleBoxFlat(Color.Transparent)),
            E<ContainerButton>().Class("OrbitraTreeRow").Box(new StyleBoxFlat(Color.Transparent)),
            E<ContainerButton>().Class("OrbitraTreeRow").PseudoNormal().Box(new StyleBoxFlat(Color.Transparent)),
            E<ContainerButton>().Class("OrbitraTreeRow").PseudoHovered().Box(optionHover),
            E<ContainerButton>().Class("OrbitraTreeRow", "selected").Box(optionHover),
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
            E<Label>().Class("OrbitraWindowTitle", "FancyWindowTitle").Font(sheet.BaseFont.GetFont(16, FontKind.Bold)),
            E<ContainerButton>().Class("OrbitraWindowClose", ContainerButton.StyleClassButton).Box(new StyleBoxFlat(Color.Transparent)),
            E<ContainerButton>().Class("OrbitraWindowClose", ContainerButton.StyleClassButton).PseudoNormal().Box(new StyleBoxFlat(Color.Transparent)).Modulate(Color.White),
            E<ContainerButton>().Class("OrbitraWindowClose", ContainerButton.StyleClassButton).PseudoHovered().Box(new StyleBoxFlat(OrbitraPalettes.PanelHighlight)),
            E<PanelContainer>().Class("OrbitraCrewCard").Panel(new StyleBoxFlat(Color.Transparent)),
            E<PanelContainer>().Class("OrbitraRoundPanel").Panel(Box(OrbitraPalettes.PanelInset.WithAlpha(0.96f), 8)),
            E<ContainerButton>().Class(ContainerButton.StyleClassButton, "OrbitraOptionRow").Box(option).MinHeight(32),
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
            E<Label>().Class("OrbitraEditorControl", "FancyWindowTitle").Font(sheet.BaseFont.GetFont(16, FontKind.Bold)),
            E<Label>().Class("OrbitraLobbyTitle").Font(sheet.BaseFont.GetFont(22, FontKind.Bold))
                .FontColor(OrbitraPalettes.Highlight.Text),
            E<Label>().Class("OrbitraLobbyMuted").Font(sheet.BaseFont.GetFont(12)).FontColor(OrbitraPalettes.IconNormal),
            E<RichTextLabel>().Class("OrbitraLobbyMuted").Font(sheet.BaseFont.GetFont(11)).FontColor(OrbitraPalettes.IconNormal),
        };
        foreach (var style in new[] { "OrbitraLobbyButton", "OrbitraLobbyPrimary", "OrbitraDangerButton", "OrbitraNavigationButton" })
        {
            rules.AddRange([
                E<ContainerButton>().Class(style, ContainerButton.StyleClassButton).Box(style == "OrbitraLobbyPrimary" ? primary : normal).MinHeight(style == "OrbitraLobbyPrimary" ? 44 : 36).Modulate(Color.White),
                E<ContainerButton>().Class(style, ContainerButton.StyleClassButton).PseudoNormal().Box(style == "OrbitraLobbyPrimary" ? primary : normal).Modulate(Color.White),
                E<ContainerButton>().Class(style, ContainerButton.StyleClassButton).PseudoHovered().Box(hover).Modulate(Color.White),
                E<ContainerButton>().Class(style, ContainerButton.StyleClassButton).PseudoPressed().Box(pressed).Modulate(Color.White),
                E<ContainerButton>().Class(style, ContainerButton.StyleClassButton).PseudoDisabled().Box(normal).Modulate(Color.White.WithAlpha(0.45f)),
                E<ContainerButton>().Class(style, ContainerButton.StyleClassButton).ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(13))
                    .FontColor(OrbitraPalettes.Highlight.Text),
            ]);
        }
        var selected = Box(OrbitraPalettes.PanelHighlight, 12);
        selected.BorderThickness = new Thickness(3, 0, 0, 0);
        selected.BorderColor = OrbitraPalettes.Highlight.Element;
        rules.Add(E<ContainerButton>().Class("OrbitraNavigationButton").PseudoPressed().Box(selected));
        rules.Add(E<ContainerButton>().Class("OrbitraDangerButton").ParentOf(E<Label>()).FontColor(sheet.NegativePalette.Text));
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
