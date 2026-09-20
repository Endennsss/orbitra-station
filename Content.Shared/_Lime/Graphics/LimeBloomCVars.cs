using Robust.Shared.Configuration;

namespace Content.Shared._Lime.Graphics;

/// <summary>
/// Клиентские настройки мирового Bloom эмиссивных объектов.
/// </summary>
[CVarDefs]
public static class LimeBloomCVars
{
    public const string QualityLow = "Low";
    public const string QualityMedium = "Medium";
    public const string QualityHigh = "High";

    /// <summary>Включает дополнительные проходы Bloom.</summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("lime.bloom_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Интенсивность финального аддитивного композита.</summary>
    public static readonly CVarDef<float> Strength =
        CVarDef.Create("lime.bloom_strength", 0.4f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Профиль разрешения и количества уровней пирамиды.</summary>
    public static readonly CVarDef<string> Quality =
        CVarDef.Create("lime.bloom_quality", QualityMedium, CVar.CLIENTONLY | CVar.ARCHIVE);
}
