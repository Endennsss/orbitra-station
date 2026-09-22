using Robust.Shared.Configuration;

namespace Content.Shared._Orbitra.Graphics;

/// <summary>
/// Клиентские настройки мирового Bloom эмиссивных объектов.
/// </summary>
[CVarDefs]
public static class OrbitraBloomCVars
{
    public const string QualityLow = "Low";
    public const string QualityMedium = "Medium";
    public const string QualityHigh = "High";

    /// <summary>Включает дополнительные проходы Bloom.</summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("orbitra.bloom_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Интенсивность финального аддитивного композита.</summary>
    public static readonly CVarDef<float> Strength =
        CVarDef.Create("orbitra.bloom_strength", 0.4f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Профиль разрешения и количества уровней пирамиды.</summary>
    public static readonly CVarDef<string> Quality =
        CVarDef.Create("orbitra.bloom_quality", QualityMedium, CVar.CLIENTONLY | CVar.ARCHIVE);
}
