using System.Numerics;
using Content.Shared._Orbitra.Ratvar;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._Orbitra.Ratvar;

/// <summary>Renders the public manifestation above walls without modifying the viewer's eye or FOV.</summary>
public sealed partial class OrbitraRatvarPresenceSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new PresenceOverlay());
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<PresenceOverlay>();
        base.Shutdown();
    }

    private sealed class PresenceOverlay : Overlay
    {
        private readonly IEntityManager _entities = IoCManager.Resolve<IEntityManager>();
        private readonly IEyeManager _eye = IoCManager.Resolve<IEyeManager>();
        public override OverlaySpace Space => OverlaySpace.WorldSpace;

        public PresenceOverlay() { ZIndex = 310; }

        protected override bool BeforeDraw(in OverlayDrawArgs args) => args.Viewport.Eye == _eye.CurrentEye;

        protected override void Draw(in OverlayDrawArgs args)
        {
            if (args.Viewport.Eye is not { } eye) return;
            var sprites = _entities.System<SpriteSystem>();
            var transforms = _entities.System<SharedTransformSystem>();
            var handle = args.WorldHandle;
            var previous = handle.GetTransform();
            var shader = handle.GetShader();
            try
            {
                handle.SetTransform(Matrix3x2.Identity);
                var query = _entities.EntityQueryEnumerator<OrbitraRatvarPresenceComponent, SpriteComponent, TransformComponent>();
                while (query.MoveNext(out var uid, out _, out var sprite, out var transform))
                {
                    if (transform.MapID != eye.Position.MapId || !sprite.Visible) continue;
                    var (position, rotation) = transforms.GetWorldPositionRotation(uid);
                    handle.UseShader(null);
                    sprites.RenderSprite((uid, sprite), handle, eye.Rotation, rotation, position);
                }
            }
            finally { handle.SetTransform(previous); handle.UseShader(shader); }
        }
    }
}
