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

        Subs.CVar(_configuration, OrbitraBloomCVars.Enabled, SetEnabled, true);
        Subs.CVar(_configuration, OrbitraBloomCVars.Strength, SetStrength, true);
        Subs.CVar(_configuration, OrbitraBloomCVars.Quality, SetQuality, true);
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

    private void SetEnabled(bool enabled)
    {
        PreviewEnabled(enabled);
    }

    private void SetStrength(float strength)
    {
        PreviewStrength(strength);
    }

    private void SetQuality(string quality)
    {
        PreviewQuality(quality);
    }
}
