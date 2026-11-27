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

    public const float MinRadius = 0.08f;
    public const float MaxRadius = 0.45f;
    public const float DefaultRadius = 0.22f;
    public const float DefaultSoftness = 0.35f;
    public const float DefaultThreshold = 0.10f;
    public const float DefaultSaturation = 1f;

    /// <summary>Включает дополнительные проходы Bloom.</summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("orbitra.bloom_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Интенсивность финального аддитивного композита.</summary>
    public static readonly CVarDef<float> Strength =
        CVarDef.Create("orbitra.bloom_strength", 0.4f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Разрешение маски и качество фильтра размытия.</summary>
    public static readonly CVarDef<string> Quality =
        CVarDef.Create("orbitra.bloom_quality", QualityMedium, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Радиус ореола в мировых тайлах.</summary>
    public static readonly CVarDef<float> Radius =
        CVarDef.Create("orbitra.bloom_radius", DefaultRadius, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Распределение свечения между серединой и краем ореола.</summary>
    public static readonly CVarDef<float> Softness =
        CVarDef.Create("orbitra.bloom_softness", DefaultSoftness, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Минимальная яркость источника с плавным переходом вблизи порога.</summary>
    public static readonly CVarDef<float> Threshold =
        CVarDef.Create("orbitra.bloom_threshold", DefaultThreshold, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Насыщенность ореола. Исходный спрайт сохраняет свой цвет.</summary>
    public static readonly CVarDef<float> Saturation =
        CVarDef.Create("orbitra.bloom_saturation", DefaultSaturation, CVar.CLIENTONLY | CVar.ARCHIVE);
}
