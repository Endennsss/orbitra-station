namespace Content.Client._Orbitra.Stylesheets;

/// <summary>Department accents; labels keep the neutral palette's readable foreground.</summary>
public static class OrbitraGhostPalette
{
    public static Color CardBackground(Color accent, float strength) =>
        Color.InterpolateBetween(OrbitraPalettes.PanelInset, accent, strength);

    public static readonly IReadOnlyDictionary<string, Color> Departments = new Dictionary<string, Color>
    {
        ["Security"] = Color.FromHex("#C85050"),
        ["Engineering"] = Color.FromHex("#D6B94C"),
        ["Cargo"] = Color.FromHex("#A47A50"),
        ["Command"] = Color.FromHex("#507DD0"),
        ["Medical"] = Color.FromHex("#65BCD7"),
        ["antagonists"] = Color.FromHex("#8F3038"),
    };
}
