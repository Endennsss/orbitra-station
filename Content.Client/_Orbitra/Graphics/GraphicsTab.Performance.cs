using Content.Client._Orbitra.UserInterface;
using Content.Shared._Orbitra.Graphics;

#pragma warning disable IDE0130 // Расширение ванильной вкладки из папки Orbitra.
namespace Content.Client.Options.UI.Tabs;

public sealed partial class GraphicsTab
{
    private void InitializeOrbitraPerformanceOptions()
    {
        string[] backgrounds = [OrbitraMenuCVars.Artwork, OrbitraMenuCVars.RandomScene, .. OrbitraMenuCVars.Scenes];
        var entries = new List<OptionDropDownCVar<string>.ValueOption>(backgrounds.Length);
        foreach (var id in backgrounds)
            entries.Add(new(id, Loc.GetString($"orbitra-options-menu-background-{id}")));
        Control.AddOptionDropDown(OrbitraMenuCVars.Background, OrbitraBackgroundDropDown, entries);
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

            UpdateOrbitraBloomOptions();
            Control.ValueChanged();
        };
    }
}
