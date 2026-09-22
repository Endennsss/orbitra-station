using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Stylesheets;

/// <summary>Нейтральная текстурная кнопка оформления HUD.</summary>
public sealed class OrbitraGrayTextureButton : TextureButton
{
    private readonly ShaderInstance _shader = IoCManager.Resolve<IPrototypeManager>()
        .Index(OrbitraHudSheetlet.GreyscaleShader).Instance();

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.UseShader(_shader);
        try
        {
            base.Draw(handle);
        }
        finally
        {
            handle.UseShader(null);
        }
    }
}
