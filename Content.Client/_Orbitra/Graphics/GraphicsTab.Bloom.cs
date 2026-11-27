using Content.Client._Orbitra.Shaders.Bloom;
using Content.Client.Options.UI;
using Content.Shared._Orbitra.Graphics;
using Robust.Shared.Configuration;

#pragma warning disable IDE0130 // Расширение ванильной вкладки из папки Orbitra.
namespace Content.Client.Options.UI.Tabs;

public sealed partial class GraphicsTab
{
    [Dependency] private IEntityManager _entityManager = default!;

    private void InitializeOrbitraBloomOptions()
    {
        var bloomOptions = new List<BaseOption>();
        var tuning = OrbitraBloomTuning;

        void Track<T>(BaseOptionCVar<T> option) where T : notnull
        {
            bloomOptions.Add(option);
            option.ImmediateValueChanged += _ => UpdateOrbitraBloomOptions();
        }

        void AddPercentSlider(CVarDef<float> cVar, OptionSlider slider)
        {
            RoundOrbitraBloomSlider(slider);
            Track(Control.AddOptionPercentSlider(cVar, slider));
        }

        Track(Control.AddOptionCheckBox(OrbitraBloomCVars.Enabled, OrbitraBloomEnabledCheckBox));
        AddPercentSlider(OrbitraBloomCVars.Strength, OrbitraBloomStrengthSlider);
        Track(Control.AddOptionDropDown(
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
            ]));

        RoundOrbitraBloomSlider(tuning.Radius);
        Track(Control.AddOption(new OptionSliderFloatCVar(Control, _cfg, OrbitraBloomCVars.Radius,
            tuning.Radius, OrbitraBloomCVars.MinRadius, OrbitraBloomCVars.MaxRadius, 1f,
            (_, value) => Loc.GetString("orbitra-options-bloom-radius-value", ("value", value)))));
        AddPercentSlider(OrbitraBloomCVars.Softness, tuning.Softness);
        AddPercentSlider(OrbitraBloomCVars.Threshold, tuning.Threshold);
        AddPercentSlider(OrbitraBloomCVars.Saturation, tuning.Saturation);

        tuning.ResetButton.OnPressed += _ =>
        {
            foreach (var option in bloomOptions)
                option.ResetToDefault();

            UpdateOrbitraBloomOptions();
            Control.ValueChanged();
        };
        Control.ValuesLoaded += UpdateOrbitraBloomOptions;
    }

    private static void RoundOrbitraBloomSlider(OptionSlider slider)
    {
        slider.Slider.RoundingDecimals = 2;
        slider.Slider.Rounded = true;
    }

    private void UpdateOrbitraBloomOptions()
    {
        var enabled = OrbitraBloomEnabledCheckBox.Pressed;
        OrbitraBloomStrengthSlider.Visible = enabled;
        OrbitraBloomQualityDropDown.Visible = enabled;

        if (!IsInsideTree || !_entityManager.TrySystem<OrbitraLightBloomSystem>(out var bloom))
            return;

        // Загрузка и сброс меняют флажок и список качества без события пользовательского ввода.
        bloom.PreviewEnabled(enabled);
        bloom.PreviewStrength(OrbitraBloomStrengthSlider.Slider.Value);
        if (OrbitraBloomQualityDropDown.Button.SelectedMetadata is string quality)
            bloom.PreviewQuality(quality);
        bloom.PreviewRadius(OrbitraBloomTuning.Radius.Slider.Value);
        bloom.PreviewSoftness(OrbitraBloomTuning.Softness.Slider.Value);
        bloom.PreviewThreshold(OrbitraBloomTuning.Threshold.Slider.Value);
        bloom.PreviewSaturation(OrbitraBloomTuning.Saturation.Slider.Value);
    }

    protected override void ExitedTree()
    {
        if (_entityManager.TrySystem<OrbitraLightBloomSystem>(out var bloom))
            bloom.RestoreConfiguredSettings();

        base.ExitedTree();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            RestoreOrbitraParticleOptions();

        base.Dispose(disposing);
    }
}
