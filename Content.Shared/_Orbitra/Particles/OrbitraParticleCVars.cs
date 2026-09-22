using Robust.Shared.Configuration;

namespace Content.Shared._Orbitra.Particles;

[CVarDefs]
public static class OrbitraParticleCVars
{
    public static readonly CVarDef<string> Quality =
        CVarDef.Create("orbitra.particles_quality", "Medium", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static (int Capacity, float Density) GetBudget(string quality) => quality switch
    {
        "Off" => (0, 0f),
        "Low" => (128, 0.5f),
        "High" => (768, 1.5f),
        _ => (384, 1f),
    };

    public static string MaterialEffect(OrbitraParticleMaterial material) => material switch
    {
        OrbitraParticleMaterial.Metal => "OrbitraParticleMetal",
        OrbitraParticleMaterial.Stone => "OrbitraParticleStone",
        OrbitraParticleMaterial.Wood => "OrbitraParticleWood",
        OrbitraParticleMaterial.Glass => "OrbitraParticleGlass",
        _ => "OrbitraParticleDust",
    };
}
