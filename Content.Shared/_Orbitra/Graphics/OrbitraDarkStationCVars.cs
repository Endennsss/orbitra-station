using System;
using Robust.Shared.Configuration;

namespace Content.Shared._Orbitra.Graphics;

/// <summary>Настройки мрачной цветокоррекции игрового мира Orbitra.</summary>
[CVarDefs]
public static class OrbitraDarkStationCVars
{
    /// <summary>Включает цветокоррекцию мрачной атмосферы станции.</summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("orbitra.dark_station_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Интенсивность затемнения и цветокоррекции, от 0 до 1.</summary>
    public static readonly CVarDef<float> Strength =
        CVarDef.Create("orbitra.dark_station_strength", 0.65f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Ограничивает пользовательское значение интенсивности безопасным диапазоном.</summary>
    public static float ClampStrength(float strength) =>
        float.IsFinite(strength) ? Math.Clamp(strength, 0f, 1f) : 0f;
}
