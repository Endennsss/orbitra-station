using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Stylesheets;

/// <summary>Обесцвечивает только декоративную текстуру панели, не её содержимое.</summary>
public sealed class OrbitraGrayStyleBoxTexture : StyleBoxTexture
{
    private readonly ShaderInstance _shader = IoCManager.Resolve<IPrototypeManager>()
        .Index(OrbitraHudSheetlet.GreyscaleShader).Instance();

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        handle.UseShader(_shader);
        try
        {
            base.DoDraw(handle, box, uiScale);
        }
        finally
        {
            handle.UseShader(null);
        }
    }
}
