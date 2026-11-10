using Robust.Shared.Configuration;

namespace Content.Shared._Orbitra.Graphics;

[CVarDefs]
public static class OrbitraGasCVars
{
    public static readonly CVarDef<bool> SmoothGases =
        CVarDef.Create("orbitra.smooth_gases", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
