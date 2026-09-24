using Content.Client._Orbitra.Stylesheets;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Explicitly scoped department accents for the ghost teleport menu.</summary>
[CommonSheetlet]
public sealed class OrbitraGhostSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var rules = new List<StyleRule>();
        AddCards(rules, null, OrbitraPalettes.PanelBorder);
        foreach (var (department, accent) in OrbitraGhostPalette.Departments)
        {
            AddCards(rules, department, accent);
            var classes = new[] { "OrbitraGroupHeader", "OrbitraCompactRow", "OrbitraGhostDepartment" + department };
            var normal = Box(OrbitraGhostPalette.CardBackground(accent, 0.10f), accent);
            rules.Add(E<Button>().Class(classes).Box(normal));
            rules.Add(E<Button>().Class(classes).PseudoNormal().Box(normal));
            rules.Add(E<Button>().Class(classes).PseudoHovered().Box(Box(OrbitraPalettes.PanelHighlight, accent)));
            rules.Add(E<Button>().Class(classes).PseudoPressed().Box(Box(OrbitraPalettes.Primary.PressedElement, accent)));
            rules.Add(E<Button>().Class(classes).PseudoDisabled().Box(Box(OrbitraPalettes.PanelInset, accent.WithAlpha(0.45f))));
        }
        return rules.ToArray();
    }

    private static void AddCards(List<StyleRule> rules, string? department, Color accent)
    {
        var classes = new List<string> { "OrbitraGhostCard", ContainerButton.StyleClassButton, OrbitraButtonStyles.Secondary };
        if (department != null)
            classes.Add("OrbitraGhostDepartment" + department);
        StyleBoxFlat Card(float tint) => new(OrbitraGhostPalette.CardBackground(accent, tint))
        {
            BorderColor = Color.InterpolateBetween(OrbitraPalettes.PanelBorder, accent, department == null ? 0 : 0.65f),
            BorderThickness = new Thickness(department == null ? 1 : 2, 1, 1, 1),
            ContentMarginLeftOverride = 10,
            ContentMarginRightOverride = 10,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6,
        };
        rules.Add(E<ContainerButton>().Class(classes.ToArray()).Box(Card(0.16f)));
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
            rules.Add(E<ContainerButton>().Class(classes.ToArray()).Pseudo(state)
                .Box(Card(state == "hover" ? 0.27f : state == "pressed" ? 0.35f : 0.16f)));
    }

    private static StyleBoxFlat Box(Color background, Color accent) => new(background)
    {
        BorderColor = accent,
        BorderThickness = new Thickness(3, 0, 0, 0),
        ContentMarginLeftOverride = 8,
        ContentMarginRightOverride = 8,
        ContentMarginTopOverride = 2,
        ContentMarginBottomOverride = 2,
    };
}
