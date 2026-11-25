using Robust.Shared.Configuration;

namespace Content.Shared._Orbitra.Graphics;

[CVarDefs]
public static class OrbitraParallaxCVars
{
    public static readonly CVarDef<bool> ProceduralParallax =
        CVarDef.Create("orbitra.procedural_parallax", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
