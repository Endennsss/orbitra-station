using Content.Client.Stylesheets.Palette;

namespace Content.Client.Stylesheets.Stylesheets;

public partial class NanotrasenStylesheet
{
    public override ColorPalette PrimaryPalette => Content.Client._Orbitra.Stylesheets.OrbitraPalettes.Primary; // Orbitra-Edit - общая серая палитра
    public override ColorPalette SecondaryPalette => Content.Client._Orbitra.Stylesheets.OrbitraPalettes.Secondary; // Orbitra-Edit - нейтральные панели
    public override ColorPalette PositivePalette => Content.Client._Orbitra.Stylesheets.OrbitraPalettes.Positive; // Orbitra-Edit - нейтральные подложки
    public override ColorPalette NegativePalette => Palettes.Red;
    public override ColorPalette HighlightPalette => Content.Client._Orbitra.Stylesheets.OrbitraPalettes.Highlight; // Orbitra-Edit - серый акцент
}
