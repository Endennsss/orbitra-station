using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.NightVision;

/// <summary>
/// Reuses the source's lighting lift without requesting a screen copy.
/// </summary>
internal sealed class OrbitraNightVisionLightOverlay : Overlay
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    private readonly OrbitraNightVisionPresentation _presentation;

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    public OrbitraNightVisionLightOverlay(OrbitraNightVisionPresentation presentation)
    {
        IoCManager.InjectDependencies(this);
        _presentation = presentation;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args) =>
        _presentation.Source != null && args.Viewport.Eye != null && args.Viewport.Eye == _eye.CurrentEye;

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var shader = handle.GetShader();
        var transform = handle.GetTransform();
        try
        {
            handle.UseShader(null);
            handle.SetTransform(System.Numerics.Matrix3x2.Identity);
            var color = _presentation.LightingColor;
            color.A *= _presentation.GetActivation(_timing.RealTime);
            handle.DrawRect(args.WorldBounds, color);
        }
        finally
        {
            handle.UseShader(shader);
            handle.SetTransform(transform);
        }
    }
}
