using Content.Shared._Orbitra.Graphics;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Orbitra.Shaders.Bloom;

/// <summary>
/// Управляет мировым Bloom эмиссивных объектов и его пользовательскими настройками.
/// </summary>
public sealed partial class OrbitraLightBloomSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;

    private OrbitraWorldBloomOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new OrbitraWorldBloomOverlay(EntityManager);
        _overlayManager.AddOverlay(_overlay);

        Subs.CVar(_configuration, OrbitraBloomCVars.Enabled, PreviewEnabled, true);
        Subs.CVar(_configuration, OrbitraBloomCVars.Strength, PreviewStrength, true);
        Subs.CVar(_configuration, OrbitraBloomCVars.Quality, PreviewQuality, true);
        Subs.CVar(_configuration, OrbitraBloomCVars.Radius, PreviewRadius, true);
        Subs.CVar(_configuration, OrbitraBloomCVars.Softness, PreviewSoftness, true);
        Subs.CVar(_configuration, OrbitraBloomCVars.Threshold, PreviewThreshold, true);
        Subs.CVar(_configuration, OrbitraBloomCVars.Saturation, PreviewSaturation, true);
    }

    public override void Shutdown()
    {
        if (_overlay != null)
        {
            _overlayManager.RemoveOverlay(_overlay);
            _overlay.Dispose();
            _overlay = null;
        }

        base.Shutdown();
    }

    /// <summary>Отменяет временный предпросмотр при закрытии настроек.</summary>
    public void RestoreConfiguredSettings()
    {
        PreviewEnabled(_configuration.GetCVar(OrbitraBloomCVars.Enabled));
        PreviewStrength(_configuration.GetCVar(OrbitraBloomCVars.Strength));
        PreviewQuality(_configuration.GetCVar(OrbitraBloomCVars.Quality));
        PreviewRadius(_configuration.GetCVar(OrbitraBloomCVars.Radius));
        PreviewSoftness(_configuration.GetCVar(OrbitraBloomCVars.Softness));
        PreviewThreshold(_configuration.GetCVar(OrbitraBloomCVars.Threshold));
        PreviewSaturation(_configuration.GetCVar(OrbitraBloomCVars.Saturation));
    }

    public void PreviewEnabled(bool enabled)
    {
        if (_overlay != null)
            _overlay.Enabled = enabled;
    }

    public void PreviewStrength(float strength)
    {
        var value = float.IsFinite(strength) ? Math.Clamp(strength, 0f, 1f) : 0f;
        if (_overlay != null)
            _overlay.Strength = value;
    }

    public void PreviewQuality(string quality)
    {
        var value = OrbitraBloomQualityExtensions.Parse(quality);
        if (_overlay != null)
            _overlay.Quality = value;
    }

    public void PreviewRadius(float radius)
    {
        if (_overlay != null)
            _overlay.Radius = ClampSetting(radius, OrbitraBloomCVars.DefaultRadius,
                OrbitraBloomCVars.MinRadius, OrbitraBloomCVars.MaxRadius);
    }

    public void PreviewSoftness(float softness)
    {
        if (_overlay != null)
            _overlay.Softness = ClampSetting(softness, OrbitraBloomCVars.DefaultSoftness);
    }

    public void PreviewThreshold(float threshold)
    {
        if (_overlay != null)
            _overlay.Threshold = ClampSetting(threshold, OrbitraBloomCVars.DefaultThreshold);
    }

    public void PreviewSaturation(float saturation)
    {
        if (_overlay != null)
            _overlay.Saturation = ClampSetting(saturation, OrbitraBloomCVars.DefaultSaturation);
    }

    private static float ClampSetting(float value, float fallback, float min = 0f, float max = 1f) =>
        float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
