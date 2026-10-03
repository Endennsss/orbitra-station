using System.Numerics;
using Content.Shared._Orbitra.Graphics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Shaders.DarkStation;

/// <summary>Накладывает мрачную цветокоррекцию только на игровой мир.</summary>
internal sealed partial class OrbitraDarkStationOverlay : Overlay
{
    internal const int DrawOrder = 175;
    private static readonly ProtoId<ShaderPrototype> ShaderId = "OrbitraDarkStation";

    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IEyeManager _eyeManager = default!;

    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public override bool RequestScreenTexture => true;

    public bool Enabled { get; set; } = true;
    public float Strength { get; set; } = 0.65f;

    public OrbitraDarkStationOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(ShaderId).InstanceUnique();
        ZIndex = DrawOrder; // После Bloom и частиц, до ПНВ и термального зрения.
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Enabled && OrbitraDarkStationCVars.ClampStrength(Strength) > 0f &&
               args.Viewport.Eye != null &&
               args.Viewport.Eye == _eyeManager.CurrentEye &&
               args.MapId != MapId.Nullspace;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        var previousShader = handle.GetShader();
        var previousTransform = handle.GetTransform();
        try
        {
            _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
            _shader.SetParameter("strength", OrbitraDarkStationCVars.ClampStrength(Strength));
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
        _shader.Dispose();
        base.DisposeBehavior();
    }
}
