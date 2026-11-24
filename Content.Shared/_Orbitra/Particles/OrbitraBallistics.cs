namespace Content.Shared._Orbitra.Particles;

/// <summary>Общие лимиты частиц и выбор материала при попаданиях пуль.</summary>
public static class OrbitraBallistics
{
    public static string Effect(OrbitraParticleMaterial material) => material switch
    {
        OrbitraParticleMaterial.Metal => "OrbitraParticleBulletMetal",
        OrbitraParticleMaterial.Stone => "OrbitraParticleBulletStone",
        OrbitraParticleMaterial.Wood => "OrbitraParticleBulletWood",
        OrbitraParticleMaterial.Glass => "OrbitraParticleBulletGlass",
        _ => "OrbitraParticleBulletDust",
    };

    public static int MarkBudget(int capacity) => capacity switch
    {
        <= 0 => 0,
        <= 128 => 16,
        <= 384 => 48,
        _ => 96,
    };
}
