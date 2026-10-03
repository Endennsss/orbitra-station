using Content.Shared._Orbitra.Graphics;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Orbitra.Shaders.DarkStation;

/// <summary>Управляет общим постэффектом мрачной атмосферы станции.</summary>
public sealed partial class OrbitraDarkStationSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;

    private OrbitraDarkStationOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new OrbitraDarkStationOverlay();
        _overlayManager.AddOverlay(_overlay);

        Subs.CVar(_configuration, OrbitraDarkStationCVars.Enabled, SetEnabled, true);
        Subs.CVar(_configuration, OrbitraDarkStationCVars.Strength, SetStrength, true);
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
        if (_overlay != null)
            _overlay.Strength = OrbitraDarkStationCVars.ClampStrength(strength);
    }

    private void SetEnabled(bool enabled)
    {
        PreviewEnabled(enabled);
    }

    private void SetStrength(float strength)
    {
        PreviewStrength(strength);
    }
}
