using System.Numerics;
using Content.Client.Graphics;
using Content.Client.Parallax;
using Content.Shared._Orbitra.Atmos;
using Content.Shared.CCVar;
using Content.Shared.Parallax.Biomes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Graphics;

public sealed partial class OrbitraEnvironmentEffectsSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprites = default!;

    private const string SootPass = "orbitra-soot";
    private static readonly ProtoId<ShaderPrototype> SootShader = "OrbitraSoot";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraSootComponent, ComponentStartup>(OnSootStartup);
        SubscribeLocalEvent<OrbitraSootComponent, ComponentShutdown>(OnSootShutdown);
        _overlays.AddOverlay(new MeteorOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        if (_overlays.TryGetOverlay<MeteorOverlay>(out var overlay))
        {
            _overlays.RemoveOverlay(overlay);
            overlay.Dispose();
        }
        base.Shutdown();
    }

    private void OnSootStartup(Entity<OrbitraSootComponent> entity, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(entity.Owner, out var sprite))
            return;

        _sprites.SetPostShader((entity.Owner, sprite),
            new SpriteComponent.PostShaderArgs(SootPass, _prototypes.Index(SootShader).Instance())
            {
                Before = ContentPostShaderIds.BeforeOutlines,
                After = ContentPostShaderIds.AfterBaseEffects,
            });
    }

    private void OnSootShutdown(Entity<OrbitraSootComponent> entity, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(entity.Owner, out var sprite))
            _sprites.RemovePostShader((entity.Owner, sprite), SootPass);
    }

    private sealed class MeteorOverlay : Overlay
    private sealed class MeteorOverlay : Overlay
    {
        private static readonly ProtoId<ShaderPrototype> MeteorShader = "OrbitraMeteorTrail";

        private readonly IEntityManager _entities;
        private readonly SharedMapSystem _maps;
        private readonly ParallaxSystem _parallax;
        private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
        private readonly IConfigurationManager _config = IoCManager.Resolve<IConfigurationManager>();

        private readonly ShaderInstance _shader =
            IoCManager.Resolve<IPrototypeManager>()
                .Index(MeteorShader)
                .InstanceUnique();

        public MeteorOverlay(IEntityManager entities)
        {
            _entities = entities;
            _maps = entities.System<SharedMapSystem>();
            _parallax = entities.System<ParallaxSystem>();
            ZIndex = ParallaxSystem.ParallaxZIndex + 1;
        }

// ХМММММММММММММММММММ

        public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

        protected override bool BeforeDraw(in OverlayDrawArgs args) =>
            args.MapId != MapId.Nullspace &&
            _config.GetCVar(CCVars.ParallaxEnabled) &&
            !_config.GetCVar(CCVars.ReducedMotion) &&
            !_entities.HasComponent<BiomeComponent>(_maps.GetMapOrInvalid(args.MapId));

        protected override void Draw(in OverlayDrawArgs args)
        {
            const double interval = 43;
            const float duration = 0.95f;
            var time = _timing.RealTime.TotalSeconds;
            var elapsed = (float) (time % interval);
            if (elapsed >= duration || _parallax.GetParallaxLayers(args.MapId).Length == 0)
                return;

            var cycle = (int) (time / interval);
            var row = 0.28f + cycle % 7 * 0.071f;
            var progress = elapsed / duration;
            var bounds = args.WorldAABB;
            var position = bounds.BottomLeft + new Vector2(bounds.Width * (0.18f + progress * 0.48f),
                bounds.Height * row - progress * bounds.Width * 0.16f);
            var direction = Vector2.Normalize(new Vector2(3f, -1f));
            const float length = 1.8f;
            var center = position - direction * (length * 0.5f);
            var rect = Box2.CenteredAround(center, new Vector2(length, 0.25f));
            var rotation = new Angle(Math.Atan2(direction.Y, direction.X));
            args.WorldHandle.UseShader(_shader);
            args.WorldHandle.DrawTextureRect(Texture.White, new Box2Rotated(rect, rotation, center),
                Color.White.WithAlpha(MathF.Sin(progress * MathF.PI)));
        }

        protected override void DisposeBehavior() => _shader.Dispose();
    }
}
