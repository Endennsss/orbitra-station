using Content.Client._Orbitra.Stylesheets;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Orbitra.Surgery;

/// <summary>Отмечает область осмотра, выбранное действие и находки в хирургической карте.</summary>
[CommonSheetlet]
public sealed class OrbitraSurgerySheetlet : Sheetlet<PalettedStylesheet>
{
    internal static readonly Color Accent = Color.FromHex("#83D5CB");
    internal static readonly Color Finding = Color.FromHex("#F0B483");
    internal static readonly Color Chart = Color.FromHex("#152729");
    internal static readonly Color Grid = Color.FromHex("#244043");
    private static readonly Color Border = Color.FromHex("#365452");

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var rules = new List<StyleRule>
        {
            E<PanelContainer>().Class("OrbitraSurgeryPatient").Panel(new StyleBoxFlat(Color.FromHex("#263B3B"))
                { BorderColor = Accent, BorderThickness = new Thickness(3, 0, 0, 0) }),
            E<PanelContainer>().Class("OrbitraSurgeryWork").Panel(new StyleBoxFlat(Color.FromHex("#243233"))
                { BorderColor = Border, BorderThickness = new Thickness(0, 1, 0, 0) }),
            E<PanelContainer>().Class("OrbitraSurgeryInset").Panel(new StyleBoxFlat(Chart)
                { BorderColor = Border, BorderThickness = new Thickness(1) }),
            E<Label>().Class("OrbitraSurgeryTitle").Font(sheet.BaseFont.GetFont(18, FontKind.Bold)).FontColor(Color.White),
            E<Label>().Class("OrbitraSurgerySection").Font(sheet.BaseFont.GetFont(14, FontKind.Bold)).FontColor(OrbitraPalettes.Highlight.Text),
            E<Label>().Class("OrbitraSurgeryCaption").Font(sheet.BaseFont.GetFont(11, FontKind.Bold)).FontColor(Accent),
            E<RichTextLabel>().Class("OrbitraSurgeryAction").Font(sheet.BaseFont.GetFont(14, FontKind.Bold)),
        };

        AddRows(rules, "OrbitraSurgeryRow", Border);
        AddRows(rules, "OrbitraSurgeryFinding", Finding);
        return rules.ToArray();
    }

    private static void AddRows(List<StyleRule> rules, string style, Color accent)
    {
        StyleBoxFlat Row(Color background, Color edge) => new(background)
        {
            BorderColor = edge,
            BorderThickness = new Thickness(2, 0, 0, 0),
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 5,
            ContentMarginTopOverride = 2,
            ContentMarginBottomOverride = 2,
        };

        var classes = new[] { style, ContainerButton.StyleClassButton };
        var normal = Row(Color.InterpolateBetween(OrbitraPalettes.PanelBackground, accent, 0.08f), accent);
        rules.Add(E<ContainerButton>().Class(classes).Box(normal).MinHeight(30));
        rules.Add(E<ContainerButton>().Class(classes).PseudoNormal().Box(normal));
        rules.Add(E<ContainerButton>().Class(classes).PseudoHovered().Box(Row(Border, Accent)));
        rules.Add(E<ContainerButton>().Class(classes).PseudoPressed().Box(Row(Color.FromHex("#345D5A"), Accent)));
        rules.Add(E<ContainerButton>().Class(classes).PseudoDisabled().Box(Row(OrbitraPalettes.PanelBackground, OrbitraPalettes.PanelBorder)));
    }
}
