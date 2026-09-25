using System.Numerics;
using Content.Client.Graphics;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.ThermalVision;

/// <summary>GPU-only exposure history; stores a single gain, never a delayed world image.</summary>
internal sealed class OrbitraThermalExposure : IDisposable
{
    private static readonly ProtoId<ShaderPrototype> ExposureShader = "OrbitraThermalExposure";
    private readonly IClyde _clyde = IoCManager.Resolve<IClyde>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly IConfigurationManager _configuration = IoCManager.Resolve<IConfigurationManager>();
    private readonly ShaderInstance _shader = IoCManager.Resolve<IPrototypeManager>()
        .Index(ExposureShader).InstanceUnique();
    private readonly OverlayResourceCache<History> _history = new();

    internal static Vector2 Adaptation(float elapsed, bool reducedMotion) => reducedMotion ? Vector2.One : new(
        1f - MathF.Exp(-Math.Max(0f, elapsed) / 0.25f),
        1f - MathF.Exp(-Math.Max(0f, elapsed) / 0.8f));

    public Texture Draw(in OverlayDrawArgs args, Texture source, TimeSpan activation)
    {
        var history = _history.GetForViewport(args.Viewport, _ => new History(_clyde));
        var now = _timing.RealTime;
        _shader.SetParameter("SCREEN_TEXTURE", source);
        _shader.SetParameter("PREVIOUS_TEXTURE", history.Previous.Texture);
        _shader.SetParameter("reset_history", !history.Initialized || history.Activation != activation);
        _shader.SetParameter("adaptation", Adaptation((float) (now - history.LastDraw).TotalSeconds,
            _configuration.GetCVar(CCVars.ReducedMotion)));

        // Замер не учитывает скрытую геометрию даже до финального прохода FOV.
        var viewport = args.Viewport;
        var size = (Vector2) viewport.Size;
        var origin = viewport.LocalToWorld(new Vector2(0, size.Y)).Position;
        var x = viewport.LocalToWorld(size).Position - origin;
        var y = viewport.LocalToWorld(Vector2.Zero).Position - origin;
        var offset = origin - viewport.Eye!.Position.Position;
        _shader.SetParameter("uv_to_eye", new Matrix3x2(x.X, x.Y, y.X, y.Y, offset.X, offset.Y));
        _shader.SetParameter("FOV_TEXTURE", viewport.FovRenderTarget.Texture);
        _shader.SetParameter("use_fov", viewport.Eye.DrawFov);

        var screen = args.RenderHandle.DrawingHandleScreen;
        var shader = screen.GetShader();
        var transform = screen.GetTransform();
        try
        {
            screen.RenderInRenderTarget(history.Next, () =>
            {
                screen.SetTransform(Matrix3x2.Identity);
                screen.UseShader(_shader);
                screen.DrawRect(UIBox2.FromDimensions(Vector2.Zero, Vector2.One), Color.White);
            }, Color.Black);
        }
        finally
        {
            screen.UseShader(shader);
            screen.SetTransform(transform);
        }
        (history.Previous, history.Next) = (history.Next, history.Previous);
        history.LastDraw = now;
        history.Activation = activation;
        history.Initialized = true;
        return history.Previous.Texture;
    }

    public void Dispose()
    {
        _history.Dispose();
        _shader.Dispose();
    }

    private sealed class History : IDisposable
    {
        public IRenderTexture Previous;
        public IRenderTexture Next;
        public TimeSpan Activation;
        public TimeSpan LastDraw;
        public bool Initialized;

        public History(IClyde clyde)
        {
            // R32F сохраняет плавность медленной адаптации без округления до восьми бит.
            var format = new RenderTargetFormatParameters(RenderTargetColorFormat.R32F);
            Previous = clyde.CreateRenderTarget(Vector2i.One, format, name: "orbitra-thermal-exposure-a");
            Next = clyde.CreateRenderTarget(Vector2i.One, format, name: "orbitra-thermal-exposure-b");
        }

        public void Dispose()
        {
            Previous.Dispose();
            Next.Dispose();
        }
    }
}
