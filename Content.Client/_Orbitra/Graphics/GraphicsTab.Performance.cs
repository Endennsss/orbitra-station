using Content.Shared._Orbitra.Graphics;

#pragma warning disable IDE0130 // Расширение ванильной вкладки из папки Orbitra.
namespace Content.Client.Options.UI.Tabs;

public sealed partial class GraphicsTab
{
    private void InitializeOrbitraPerformanceOptions()
    {
        Control.AddOptionCheckBox(OrbitraGasCVars.SmoothGases, OrbitraSmoothGasesCheckBox);
        OrbitraLowSpecButton.OnPressed += _ =>
        {
            OrbitraBloomEnabledCheckBox.Pressed = false;
            ParallaxLowQualityCheckBox.Pressed = true;
            AmbientOcclusionCheckBox.Pressed = false;

            var particles = OrbitraParticleQualityDropDown.Button;
            for (var i = 0; i < particles.ItemCount; i++)
            {
                if (particles.GetItemMetadata(i) is "Low")
                {
                    particles.Select(i);
                    break;
                }
            }

            UpdateOrbitraBloomVisibility();
            Control.ValueChanged();
        };
    }
}
