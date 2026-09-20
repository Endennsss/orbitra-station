using Content.Client._Lime.Shaders.Bloom;
using Content.Client.Options.UI;
using Content.Shared._Lime.Graphics;

#pragma warning disable IDE0130 // Расширение ванильной вкладки из папки Lime.
namespace Content.Client.Options.UI.Tabs;

public sealed partial class GraphicsTab
{
    [Dependency] private IEntityManager _entityManager = default!;

    private void InitializeLimeBloomOptions()
    {
        var enabled = Control.AddOptionCheckBox(LimeBloomCVars.Enabled, LimeBloomEnabledCheckBox);
        var strength = Control.AddOptionPercentSlider(LimeBloomCVars.Strength, LimeBloomStrengthSlider);
        var quality = Control.AddOptionDropDown(
            LimeBloomCVars.Quality,
            LimeBloomQualityDropDown,
            [
                new OptionDropDownCVar<string>.ValueOption(
                    LimeBloomCVars.QualityLow,
                    Loc.GetString("lime-options-bloom-quality-low")),
                new OptionDropDownCVar<string>.ValueOption(
                    LimeBloomCVars.QualityMedium,
                    Loc.GetString("lime-options-bloom-quality-medium")),
                new OptionDropDownCVar<string>.ValueOption(
                    LimeBloomCVars.QualityHigh,
                    Loc.GetString("lime-options-bloom-quality-high")),
            ]);

        LimeBloomStrengthSlider.Slider.Rounded = true;
        LimeBloomStrengthSlider.Slider.RoundingDecimals = 2;

        enabled.ImmediateValueChanged += PreviewLimeBloomEnabled;
        strength.ImmediateValueChanged += PreviewLimeBloomStrength;
        quality.ImmediateValueChanged += PreviewLimeBloomQuality;
        LimeBloomEnabledCheckBox.OnToggled += _ => UpdateLimeBloomVisibility();
    }

    private void UpdateLimeBloomVisibility()
    {
        LimeBloomStrengthSlider.Visible = LimeBloomEnabledCheckBox.Pressed;
        LimeBloomQualityDropDown.Visible = LimeBloomEnabledCheckBox.Pressed;
    }

    private void PreviewLimeBloomEnabled(bool enabled)
    {
        _entityManager.System<LimeLightBloomSystem>().PreviewEnabled(enabled);
    }

    private void PreviewLimeBloomStrength(float strength)
    {
        _entityManager.System<LimeLightBloomSystem>().PreviewStrength(strength);
    }

    private void PreviewLimeBloomQuality(string quality)
    {
        _entityManager.System<LimeLightBloomSystem>().PreviewQuality(quality);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            PreviewLimeBloomEnabled(_cfg.GetCVar(LimeBloomCVars.Enabled));
            PreviewLimeBloomStrength(_cfg.GetCVar(LimeBloomCVars.Strength));
            PreviewLimeBloomQuality(_cfg.GetCVar(LimeBloomCVars.Quality));
        }

        base.Dispose(disposing);
    }
}
