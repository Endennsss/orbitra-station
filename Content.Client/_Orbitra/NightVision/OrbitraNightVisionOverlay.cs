using System.Numerics;
using Content.Client.Graphics;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.NightVision;

/// <summary>
/// Applies device optics to the world after Bloom and before the engine's FOV mask.
/// </summary>
internal sealed partial class OrbitraNightVisionOverlay : Overlay
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IClyde _clyde = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "OrbitraNightVision";
    private readonly OrbitraNightVisionPresentation _presentation;
    private readonly ShaderInstance _shader;
    private readonly ShaderInstance _exposure;
    private readonly OverlayResourceCache<ExposureResources> _resources = new();
    private static readonly ProtoId<ShaderPrototype> ExposureShader = "OrbitraNightVisionExposure";

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public override bool RequestScreenTexture => true;

    public OrbitraNightVisionOverlay(OrbitraNightVisionPresentation presentation)
    {
        IoCManager.InjectDependencies(this);
        _presentation = presentation;
        _shader = _prototype.Index(Shader).InstanceUnique();
        _exposure = _prototype.Index(ExposureShader).InstanceUnique();
        ZIndex = 200; // После Orbitra Bloom (100), до штатного FOV.
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args) =>
        _presentation.Source != null && args.Viewport.Eye != null && args.Viewport.Eye == _eye.CurrentEye;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        var previousShader = handle.GetShader();
        var previousTransform = handle.GetTransform();
        try
        {
            var size = (Vector2) args.Viewport.Size;
            SetVisibilityParameters(_shader, args.Viewport);
            var exposure = DrawExposure(in args);
            _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
            _shader.SetParameter("EXPOSURE_TEXTURE", exposure);
            _shader.SetParameter("viewport_size", size);
            _shader.SetParameter("aperture_radii", OrbitraNightVisionPresentation.GetApertureRadii(size));
            _shader.SetParameter("edge_fraction", OrbitraNightVisionPresentation.EdgeFraction);
            _shader.SetParameter("activation", _presentation.GetActivation(_timing.RealTime));
            _shader.SetParameter("noise_enabled", !_configuration.GetCVar(CCVars.DisableNightVisionNoise));
            _shader.SetParameter("noise_frame", (float) (Math.Floor(_timing.RealTime.TotalSeconds * 24) % 4096));
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(_shader);
            handle.DrawRect(args.WorldBounds, Color.White);
        }
        finally
        {
            handle.UseShader(previousShader);
            handle.SetTransform(previousTransform);
        }
    }

    protected override void DisposeBehavior()
    {
        ResetExposure();
        _exposure.Dispose();
        _shader.Dispose();
        base.DisposeBehavior();
    }

    internal void ResetExposure() => _resources.Dispose();

    private Texture DrawExposure(in OverlayDrawArgs args)
    {
        var cache = _resources.GetForViewport(args.Viewport, _ => new ExposureResources(_clyde));
        var now = _timing.RealTime;
        var reset = !cache.Initialized || cache.Revision != _presentation.Revision;
        _exposure.SetParameter("SCREEN_TEXTURE", ScreenTexture!);
        _exposure.SetParameter("PREVIOUS_TEXTURE", cache.Previous.Texture);
        _exposure.SetParameter("reset_history", reset);
        _exposure.SetParameter("adaptation", OrbitraNightVisionPresentation.GetAdaptationFactors(
            (float) (now - cache.LastDraw).TotalSeconds));
        _exposure.SetParameter("viewport_size", (Vector2) args.Viewport.Size);
        _exposure.SetParameter("aperture_radii", OrbitraNightVisionPresentation.GetApertureRadii(args.Viewport.Size));
        SetVisibilityParameters(_exposure, args.Viewport);

        // Храним только одно число, а не предыдущий кадр: движения не оставляют шлейфов.
        var screen = args.RenderHandle.DrawingHandleScreen;
        var shader = screen.GetShader();
        var transform = screen.GetTransform();
        try
        {
            screen.RenderInRenderTarget(cache.Next, () =>
            {
                screen.SetTransform(Matrix3x2.Identity);
                screen.UseShader(_exposure);
                screen.DrawRect(UIBox2.FromDimensions(Vector2.Zero, Vector2.One), Color.White);
            }, Color.Black);
        }
        finally
        {
            screen.UseShader(shader);
            screen.SetTransform(transform);
        }

        (cache.Previous, cache.Next) = (cache.Next, cache.Previous);
        cache.LastDraw = now;
        cache.Revision = _presentation.Revision;
        cache.Initialized = true;
        return cache.Previous.Texture;
    }

    private static void SetVisibilityParameters(ShaderInstance shader, IClydeViewport viewport)
    {
        // UV экранной текстуры начинается снизу слева; LocalToWorld принимает координаты сверху слева.
        var size = (Vector2) viewport.Size;
        var origin = viewport.LocalToWorld(new Vector2(0, size.Y)).Position;
        var x = viewport.LocalToWorld(size).Position - origin;
        var y = viewport.LocalToWorld(Vector2.Zero).Position - origin;
        var offset = origin - viewport.Eye!.Position.Position;
        shader.SetParameter("uv_to_eye", new Matrix3x2(x.X, x.Y, y.X, y.Y, offset.X, offset.Y));
        shader.SetParameter("FOV_TEXTURE", viewport.FovRenderTarget.Texture);
        shader.SetParameter("use_fov", viewport.Eye.DrawFov);
    }

    private sealed class ExposureResources : IDisposable
    {
        public IRenderTexture Previous;
        public IRenderTexture Next;
        public bool Initialized;
        public uint Revision;
        public TimeSpan LastDraw;

        public ExposureResources(IClyde clyde)
        {
            // Float исключает ступеньки и остановку медленной адаптации из-за округления до 8 бит.
            var format = new RenderTargetFormatParameters(RenderTargetColorFormat.R32F);
            Previous = clyde.CreateRenderTarget(Vector2i.One, format, name: "orbitra-nv-exposure-a");
            Next = clyde.CreateRenderTarget(Vector2i.One, format, name: "orbitra-nv-exposure-b");
        }

        public void Dispose()
        {
            Previous.Dispose();
            Next.Dispose();
        }
    }
}
