using Content.Client._Orbitra.Shaders.Bloom;
using Content.Client.Options.UI;
using Content.Shared._Orbitra.Graphics;

#pragma warning disable IDE0130 // Расширение ванильной вкладки из папки Orbitra.
namespace Content.Client.Options.UI.Tabs;

public sealed partial class GraphicsTab
{
    [Dependency] private IEntityManager _entityManager = default!;

    private void InitializeOrbitraBloomOptions()
    {
        var enabled = Control.AddOptionCheckBox(OrbitraBloomCVars.Enabled, OrbitraBloomEnabledCheckBox);
        var strength = Control.AddOptionPercentSlider(OrbitraBloomCVars.Strength, OrbitraBloomStrengthSlider);
        var quality = Control.AddOptionDropDown(
            OrbitraBloomCVars.Quality,
            OrbitraBloomQualityDropDown,
            [
                new OptionDropDownCVar<string>.ValueOption(
                    OrbitraBloomCVars.QualityLow,
                    Loc.GetString("orbitra-options-bloom-quality-low")),
                new OptionDropDownCVar<string>.ValueOption(
                    OrbitraBloomCVars.QualityMedium,
                    Loc.GetString("orbitra-options-bloom-quality-medium")),
                new OptionDropDownCVar<string>.ValueOption(
                    OrbitraBloomCVars.QualityHigh,
                    Loc.GetString("orbitra-options-bloom-quality-high")),
            ]);

        OrbitraBloomStrengthSlider.Slider.Rounded = true;
        OrbitraBloomStrengthSlider.Slider.RoundingDecimals = 2;

        enabled.ImmediateValueChanged += PreviewOrbitraBloomEnabled;
        strength.ImmediateValueChanged += PreviewOrbitraBloomStrength;
        quality.ImmediateValueChanged += PreviewOrbitraBloomQuality;
        OrbitraBloomEnabledCheckBox.OnToggled += _ => UpdateOrbitraBloomVisibility();
    }

    private void UpdateOrbitraBloomVisibility()
    {
        OrbitraBloomStrengthSlider.Visible = OrbitraBloomEnabledCheckBox.Pressed;
        OrbitraBloomQualityDropDown.Visible = OrbitraBloomEnabledCheckBox.Pressed;
    }

    private void PreviewOrbitraBloomEnabled(bool enabled)
    {
        _entityManager.System<OrbitraLightBloomSystem>().PreviewEnabled(enabled);
    }

    private void PreviewOrbitraBloomStrength(float strength)
    {
        _entityManager.System<OrbitraLightBloomSystem>().PreviewStrength(strength);
    }

    private void PreviewOrbitraBloomQuality(string quality)
    {
        _entityManager.System<OrbitraLightBloomSystem>().PreviewQuality(quality);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            RestoreOrbitraParticleOptions();
            PreviewOrbitraBloomEnabled(_cfg.GetCVar(OrbitraBloomCVars.Enabled));
            PreviewOrbitraBloomStrength(_cfg.GetCVar(OrbitraBloomCVars.Strength));
            PreviewOrbitraBloomQuality(_cfg.GetCVar(OrbitraBloomCVars.Quality));
        }

        base.Dispose(disposing);
    }
}
