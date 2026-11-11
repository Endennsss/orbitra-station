using System.Numerics;
using Content.Client.Light.EntitySystems;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Graphics;

public sealed partial class OrbitraAtmosphereGradeSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IEyeManager _eyes = default!;
    [Dependency] private GridStencilSystem _stencil = default!;

    private AtmosphereGradeOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new AtmosphereGradeOverlay(_prototypes, _eyes, _stencil);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay(_overlay);
        _overlay.Dispose();
        base.Shutdown();
    }

    private sealed class AtmosphereGradeOverlay : Overlay
    {
        private static readonly ProtoId<ShaderPrototype> GradeShader = "OrbitraAtmosphereGrade";
        private readonly ShaderInstance _shader;
        private readonly IEyeManager _eyes;
        private readonly GridStencilSystem _stencil;

        public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
        public override bool RequestScreenTexture => true;

        public AtmosphereGradeOverlay(IPrototypeManager prototypes, IEyeManager eyes, GridStencilSystem stencil)
        {
            _shader = prototypes.Index(GradeShader).InstanceUnique();
            _eyes = eyes;
            _stencil = stencil;
            // После Bloom, перед специальными режимами зрения.
            ZIndex = 150;
        }

        protected override bool BeforeDraw(in OverlayDrawArgs args) =>
            args.MapId != MapId.Nullspace && args.Viewport.Eye != null && args.Viewport.Eye == _eyes.CurrentEye;

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
                _shader.SetParameter("WORLD_LIGHT", args.Viewport.LightRenderTarget.Texture);
                _shader.SetParameter("GRID_MASK", _stencil.GetNonSpaceStencil(in args).Texture);
                handle.UseShader(_shader);
                handle.SetTransform(Matrix3x2.Identity);
                handle.DrawRect(args.WorldBounds, Color.White);
            }
            finally
            {
                handle.UseShader(previousShader);
                handle.SetTransform(previousTransform);
            }
        }

        protected override void DisposeBehavior() => _shader.Dispose();
    }
}
