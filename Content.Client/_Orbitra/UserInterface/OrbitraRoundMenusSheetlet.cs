using Content.Client._Orbitra.Stylesheets;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Orbitra.UserInterface;

[CommonSheetlet]
public sealed class OrbitraRoundMenusSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var card = Panel(OrbitraPalettes.PanelBackground, 10);
        var row = Panel(OrbitraPalettes.PanelBackground, 6);
        // Orbitra-Edit - подсветка новых ролей сохраняет отступы обычной кнопки во всех состояниях.
        var availableRoles = Panel(OrbitraPalettes.PanelHighlight, OrbitraUiMetrics.Medium);
        availableRoles.SetContentMarginOverride(StyleBox.Margin.Vertical, 6);
        return
        [
            E<PanelContainer>().Class("OrbitraRoleCard").Panel(card),
            E<PanelContainer>().Class("OrbitraGhostBar").Panel(Panel(OrbitraPalettes.PanelInset, 6)),
            E<PanelContainer>().Class("OrbitraManifestPanel").Panel(row),
            E<RichTextLabel>().Class("OrbitraManifestAntagonist").FontColor(sheet.NegativePalette.Text),
            E<Button>().Class("OrbitraGhostRolesAvailable", "OrbitraLobbyButton").Box(availableRoles),
            E<Button>().Class("OrbitraGhostRolesAvailable", "OrbitraLobbyButton").PseudoNormal().Box(availableRoles),
            E<Button>().Class("OrbitraGhostRolesAvailable", "OrbitraLobbyButton").PseudoHovered().Box(availableRoles),
            E<Button>().Class("OrbitraGhostRolesAvailable", "OrbitraLobbyButton").PseudoPressed().Box(availableRoles),
            E<Button>().Class("OrbitraGhostRolesAvailable", "OrbitraLobbyButton").PseudoDisabled().Box(availableRoles),
        ];
    }

    private static StyleBoxFlat Panel(Color color, float margin) => new(color)
    {
        BorderColor = OrbitraPalettes.PanelBorder,
        BorderThickness = new Thickness(1),
        ContentMarginLeftOverride = margin,
        ContentMarginRightOverride = margin,
        ContentMarginTopOverride = margin,
        ContentMarginBottomOverride = margin,
    };
}
