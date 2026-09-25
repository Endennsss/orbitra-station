using System.Numerics;
using Content.Shared.CCVar;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.ThermalVision;

/// <summary>Common ownership and activation for optics; never renders into unrelated camera viewports.</summary>
internal abstract class OrbitraThermalOverlay : Overlay
{
    protected readonly OrbitraThermalVisionSystem Thermal;
    protected readonly ShaderInstance Shader;
    private readonly IEyeManager _eye = IoCManager.Resolve<IEyeManager>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly IConfigurationManager _configuration = IoCManager.Resolve<IConfigurationManager>();

    protected OrbitraThermalOverlay(OrbitraThermalVisionSystem thermal, string shader)
    {
        Thermal = thermal;
        Shader = IoCManager.Resolve<IPrototypeManager>().Index<ShaderPrototype>(shader).InstanceUnique();
        ZIndex = 300;
    }

    protected float Activation => _configuration.GetCVar(CCVars.ReducedMotion) ? 1f :
        Math.Clamp((float) (_timing.RealTime - Thermal.ActivatedAt).TotalSeconds / 0.18f, 0, 1);

    protected override bool BeforeDraw(in OverlayDrawArgs args) => Thermal.IsActive() &&
        args.Viewport.Eye == _eye.CurrentEye && args.Viewport.Eye?.Position.MapId == Thermal.Contacts!.Map;

    protected override void DisposeBehavior()
    {
        Shader.Dispose();
        base.DisposeBehavior();
    }
}

/// <summary>Desaturates the world before FOV, leaving hidden geometry and HUD untouched.</summary>
internal sealed class OrbitraThermalWorldOverlay(OrbitraThermalVisionSystem thermal)
    : OrbitraThermalOverlay(thermal, "OrbitraThermalWorld")
{
    private readonly OrbitraThermalExposure _exposure = new();
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public override bool RequestScreenTexture => true;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null) return;
        var handle = args.WorldHandle;
        var previous = handle.GetShader();
        var transform = handle.GetTransform();
        try
        {
            Shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
            Shader.SetParameter("EXPOSURE_TEXTURE", _exposure.Draw(in args, ScreenTexture, Thermal.ActivatedAt));
            Shader.SetParameter("activation", Activation);
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(Shader);
            handle.DrawRect(args.WorldBounds, Color.White);
        }
        finally { handle.UseShader(previous); handle.SetTransform(transform); }
    }

    protected override void DisposeBehavior()
    {
        _exposure.Dispose();
        base.DisposeBehavior();
    }
}

/// <summary>Re-renders authorized PVS sprites above FOV using their normal animation and transform.</summary>
internal sealed class OrbitraThermalContactOverlay : OrbitraThermalOverlay
{
    private readonly SpriteSystem _sprites = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();
    private readonly SharedTransformSystem _transform = IoCManager.Resolve<IEntityManager>().System<SharedTransformSystem>();
    private readonly SpriteComponent.PostShaderEntry[] _passes;

    public OrbitraThermalContactOverlay(OrbitraThermalVisionSystem thermal) : base(thermal, "OrbitraThermalContact")
    {
        // Передаём проход только этому вызову рендера, не меняя постэффекты самого спрайта.
        _passes = [new SpriteComponent.PostShaderEntry("OrbitraThermalContact", Shader)];
    }

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (Thermal.Contacts is not { } contacts || args.Viewport.Eye is not { } eye) return;
        var handle = args.WorldHandle;
        var previous = handle.GetShader();
        var transform = handle.GetTransform();
        try
        {
            handle.SetTransform(Matrix3x2.Identity);
            Shader.SetParameter("activation", Activation);
            Shader.SetParameter("glow_width", Math.Clamp(args.Viewport.RenderScale.X / eye.Zoom.X, 0.5f, 4f));
            foreach (var contact in contacts.Contacts)
            {
                if (!Thermal.TryGetContactSprite(contact, out var sprite)) continue;
                var (position, rotation) = _transform.GetWorldPositionRotation(sprite.Owner);
                handle.UseShader(null);
                _sprites.RenderSprite(sprite, handle, eye.Rotation, rotation, position, _passes);
            }
        }
        finally { handle.UseShader(previous); handle.SetTransform(transform); }
    }
}
