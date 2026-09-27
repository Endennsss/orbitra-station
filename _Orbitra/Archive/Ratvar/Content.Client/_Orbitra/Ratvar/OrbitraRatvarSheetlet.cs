using Content.Client._Orbitra.Stylesheets;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Orbitra.Ratvar;

/// <summary>Shared Orbitra panels with a restrained brass border for scripture cards.</summary>
[CommonSheetlet]
public sealed class OrbitraRatvarSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config) =>
    [
        E<PanelContainer>().Class("OrbitraRatvarCard").Panel(new StyleBoxFlat(OrbitraPalettes.PanelBackground)
        {
            BorderColor = Color.FromHex("#76613F"),
            BorderThickness = new Thickness(2, 1, 1, 1),
            ContentMarginLeftOverride = 10,
            ContentMarginRightOverride = 10,
            ContentMarginTopOverride = 8,
            ContentMarginBottomOverride = 8,
        }),
    ];
}
