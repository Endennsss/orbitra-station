using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;

namespace Content.Client._Orbitra.Pda;

/// <summary>Рисует детали корпуса из основного слоя спрайта КПК.</summary>
public sealed class OrbitraPdaTrim : Control
{
    internal const int Inset = 13;
    internal Texture? Frame { get; private set; }

    internal void SetSprite(SpriteComponent.Layer layer)
    {
        Frame = layer.ActualRsi is { } rsi &&
            rsi.Path.CanonPath == "/Textures/Objects/Devices/pda.rsi" &&
            rsi.TryGetState(layer.State, out var state)
                ? state.GetFrame(RsiDirection.South, 0)
                : null;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (Frame is not { Width: 32, Height: 32 } texture)
            return;

        var width = PixelWidth;
        var height = PixelHeight;
        var inset = MathF.Round(Inset * UIScale);
        var edge = MathF.Round(2 * UIScale);
        if (width <= 2 * inset || height <= 2 * inset)
            return;

        // Четыре угла сохраняют скосы. Центральные участки обода тянутся вдоль экрана.
        DrawRegion(new UIBox2(edge, edge, inset, inset), new UIBox2(8, 4, 10, 7));
        DrawRegion(new UIBox2(inset, edge, width - inset, inset), new UIBox2(10, 4, 22, 7));
        DrawRegion(new UIBox2(width - inset, edge, width - edge, inset), new UIBox2(22, 4, 24, 7));
        DrawRegion(new UIBox2(edge, inset, inset, height - inset), new UIBox2(8, 7, 10, 24));
        DrawRegion(new UIBox2(width - inset, inset, width - edge, height - inset), new UIBox2(22, 7, 24, 24));

        // Ряд с контактами оставляем в спрайте предмета, чтобы не растягивать его по всей ширине окна.
        DrawRegion(new UIBox2(edge, height - inset, inset, height - edge), new UIBox2(8, 25, 10, 27));
        DrawRegion(new UIBox2(inset, height - inset, width - inset, height - edge), new UIBox2(10, 25, 22, 27));
        DrawRegion(new UIBox2(width - inset, height - inset, width - edge, height - edge), new UIBox2(22, 25, 24, 27));

        void DrawRegion(UIBox2 target, UIBox2 source) =>
            handle.DrawTextureRectRegion(texture, target, source, Color.White.WithAlpha(0.46f));
    }
}
