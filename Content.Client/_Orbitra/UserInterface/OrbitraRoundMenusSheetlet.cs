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
        return
        [
            E<PanelContainer>().Class("OrbitraRoleCard").Panel(card),
            E<PanelContainer>().Class("OrbitraGhostBar").Panel(Panel(OrbitraPalettes.PanelInset, 6)),
            E<PanelContainer>().Class("OrbitraManifestPanel").Panel(row),
            E<RichTextLabel>().Class("OrbitraManifestAntagonist").FontColor(sheet.NegativePalette.Text),
            E<Button>().Class("OrbitraGhostRolesAvailable", "OrbitraLobbyButton").Box(Panel(OrbitraPalettes.PanelHighlight, 8)),
            E<Button>().Class("OrbitraGhostRolesAvailable", "OrbitraLobbyButton").PseudoNormal().Box(Panel(OrbitraPalettes.PanelHighlight, 8)),
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
