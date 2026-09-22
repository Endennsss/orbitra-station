using Content.Client._Orbitra.Particles;
using Content.Shared._Orbitra.Particles;

namespace Content.Client.Options.UI.Tabs;

public sealed partial class GraphicsTab
{
    private void InitializeOrbitraParticleOptions()
    {
        var quality = Control.AddOptionDropDown(OrbitraParticleCVars.Quality, OrbitraParticleQualityDropDown,
        [
            new OptionDropDownCVar<string>.ValueOption("Off", Loc.GetString("orbitra-options-particles-off")),
            new OptionDropDownCVar<string>.ValueOption("Low", Loc.GetString("orbitra-options-particles-low")),
            new OptionDropDownCVar<string>.ValueOption("Medium", Loc.GetString("orbitra-options-particles-medium")),
            new OptionDropDownCVar<string>.ValueOption("High", Loc.GetString("orbitra-options-particles-high")),
        ]);
        quality.ImmediateValueChanged += PreviewOrbitraParticles;
    }

    private void PreviewOrbitraParticles(string quality)
    {
        if (_entityManager.TrySystem<OrbitraParticleSystem>(out var particles))
            particles.PreviewQuality(quality);
    }

    private void RestoreOrbitraParticleOptions() =>
        PreviewOrbitraParticles(_cfg.GetCVar(OrbitraParticleCVars.Quality));
}
