namespace Content.Client.Stylesheets.Palette;

/// <summary>
///     Stores all style palettes in one accessible location
/// </summary>
/// <remarks>
///     Technically not limited to only colors, can store like, standard padding amounts, and font sizes, maybe?
/// </remarks>
public static class Palettes
{
    // Orbitra edit start - старые окна также используют общую серую тему
    public static readonly ColorPalette Navy = _Orbitra.Stylesheets.OrbitraPalettes.Primary;
    public static readonly ColorPalette Cyan = _Orbitra.Stylesheets.OrbitraPalettes.Secondary;
    public static readonly ColorPalette Slate = _Orbitra.Stylesheets.OrbitraPalettes.Secondary;
    public static readonly ColorPalette Neutral = _Orbitra.Stylesheets.OrbitraPalettes.Primary;
    // Orbitra edit end

    // status tones
    public static readonly ColorPalette Red = ColorPalette.FromHexBase("#b62124", chromaShift: 0.02f);
    public static readonly ColorPalette Amber = ColorPalette.FromHexBase("#c18e36");
    public static readonly ColorPalette Green = ColorPalette.FromHexBase("#3c854a");
    public static readonly StatusPalette Status = new([Red.Base, Amber.Base, Green.Base]);

    // highlight tones
    public static readonly ColorPalette Gold = _Orbitra.Stylesheets.OrbitraPalettes.Highlight; // Orbitra-Edit - нейтральный акцент
    public static readonly ColorPalette Maroon = ColorPalette.FromHexBase("#9b2236");

    // Intended to be used with `ModulateSelf` to darken / lighten something
    public static readonly ColorPalette AlphaModulate = ColorPalette.FromHexBase("#ffffff");

}
