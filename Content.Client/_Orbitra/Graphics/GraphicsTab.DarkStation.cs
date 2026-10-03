using Content.Client._Orbitra.Shaders.DarkStation;
using Content.Client.Options.UI;
using Content.Shared._Orbitra.Graphics;

#pragma warning disable IDE0130 // Расширение ванильной вкладки из папки Orbitra.
namespace Content.Client.Options.UI.Tabs;

public sealed partial class GraphicsTab
{
    private void InitializeOrbitraDarkStationOptions()
    {
        var enabled = Control.AddOptionCheckBox(
            OrbitraDarkStationCVars.Enabled,
            OrbitraDarkStationEnabledCheckBox);
        var strength = Control.AddOptionPercentSlider(
            OrbitraDarkStationCVars.Strength,
            OrbitraDarkStationStrengthSlider);

        OrbitraDarkStationStrengthSlider.Slider.Rounded = true;
        OrbitraDarkStationStrengthSlider.Slider.RoundingDecimals = 2;

        enabled.ImmediateValueChanged += PreviewOrbitraDarkStationEnabled;
        strength.ImmediateValueChanged += PreviewOrbitraDarkStationStrength;
        OrbitraDarkStationEnabledCheckBox.OnToggled += _ => UpdateOrbitraDarkStationVisibility();
    }

    private void UpdateOrbitraDarkStationVisibility()
    {
        OrbitraDarkStationStrengthSlider.Visible = OrbitraDarkStationEnabledCheckBox.Pressed;
    }

    private void PreviewOrbitraDarkStationEnabled(bool enabled)
    {
        if (_entityManager.TrySystem<OrbitraDarkStationSystem>(out var darkness))
            darkness.PreviewEnabled(enabled);
    }

    private void PreviewOrbitraDarkStationStrength(float strength)
    {
        if (_entityManager.TrySystem<OrbitraDarkStationSystem>(out var darkness))
            darkness.PreviewStrength(strength);
    }

    private void RestoreOrbitraDarkStationOptions()
    {
        PreviewOrbitraDarkStationEnabled(_cfg.GetCVar(OrbitraDarkStationCVars.Enabled));
        PreviewOrbitraDarkStationStrength(_cfg.GetCVar(OrbitraDarkStationCVars.Strength));
    }
}
