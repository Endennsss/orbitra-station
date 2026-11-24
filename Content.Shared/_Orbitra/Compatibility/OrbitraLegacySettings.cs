using Content.Shared._Orbitra.Graphics;
using Content.Shared._Orbitra.Particles;
using Robust.Shared.Configuration;

namespace Content.Shared._Orbitra.Compatibility;

/// <summary>Импортирует настройки прежнего названия проекта, сохраняя явно заданные новые значения.</summary>
[CVarDefs]
public static class OrbitraLegacySettings
{
    public static readonly CVarDef<bool> OldBloomEnabled = CVarDef.Create("lime.bloom_enabled", true, CVar.CLIENTONLY);
    public static readonly CVarDef<float> OldBloomStrength = CVarDef.Create("lime.bloom_strength", 0.4f, CVar.CLIENTONLY);
    public static readonly CVarDef<string> OldBloomQuality = CVarDef.Create("lime.bloom_quality", "Medium", CVar.CLIENTONLY);
    public static readonly CVarDef<string> OldParticlesQuality = CVarDef.Create("lime.particles_quality", "Medium", CVar.CLIENTONLY);

    /// <summary>Выполняется после загрузки настроек до подписок систем интерфейса.</summary>
    public static void Import(IConfigurationManager config)
    {
        Import(config, OldBloomEnabled, OrbitraBloomCVars.Enabled, true);
        Import(config, OldBloomStrength, OrbitraBloomCVars.Strength, 0.4f);
        Import(config, OldBloomQuality, OrbitraBloomCVars.Quality, "Medium");
        Import(config, OldParticlesQuality, OrbitraParticleCVars.Quality, "Medium");
    }

    private static void Import<T>(IConfigurationManager config, CVarDef<T> old, CVarDef<T> current, T defaultValue) where T : notnull
    {
        // OverrideDefault не меняет явно загруженное значение или параметр командной строки.
        config.OverrideDefault(current, config.GetCVar(old));
        config.SetCVar(current, config.GetCVar(current));
        config.OverrideDefault(current, defaultValue);
    }
}
