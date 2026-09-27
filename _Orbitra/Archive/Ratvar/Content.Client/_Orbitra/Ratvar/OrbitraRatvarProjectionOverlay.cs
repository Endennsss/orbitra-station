using System.Numerics;
using Content.Shared._Orbitra.Ratvar;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._Orbitra.Ratvar;

/// <summary>A steady brass connection beneath ordinary lighting and FOV, with no screen-texture pass.</summary>
internal sealed class OrbitraRatvarProjectionOverlay : Overlay
{
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly EntityQuery<TransformComponent> _transforms;
    private readonly EntityQuery<MetaDataComponent> _metadata;
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public OrbitraRatvarProjectionOverlay(IEntityManager entities)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _transforms = entities.GetEntityQuery<TransformComponent>();
        _metadata = entities.GetEntityQuery<MetaDataComponent>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var oldTransform = handle.GetTransform();
        var oldShader = handle.GetShader();
        try
        {
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(null);
            var query = _entities.EntityQueryEnumerator<OrbitraRatvarProjectionVisualComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var visual, out var projection))
            {
                if (visual.Anchor is not { } anchor || !VisibleEntity(uid) || !VisibleEntity(anchor) ||
                    !_transforms.TryComp(anchor, out var crystal) || projection.MapID != args.MapId || crystal.MapID != args.MapId)
                    continue;
                var start = _transform.GetWorldPosition(crystal);
                var end = _transform.GetWorldPosition(projection);
                var distance = end - start;
                if (distance.LengthSquared() < 0.01f) continue;
                var midpoint = (start + end) / 2;
                var angle = distance.ToWorldAngle();
                var length = distance.Length() / 2;
                // Три статичных слоя вместо мерцания или создаваемых каждый кадр ресурсов.
                handle.DrawRect(new Box2Rotated(new Box2(-0.07f, -length, 0.07f, length).Translated(midpoint), angle, midpoint), visual.Color.WithAlpha(0.1f));
                handle.DrawRect(new Box2Rotated(new Box2(-0.025f, -length, 0.025f, length).Translated(midpoint), angle, midpoint), visual.Color);
                handle.DrawRect(new Box2Rotated(new Box2(-0.008f, -length, 0.008f, length).Translated(midpoint), angle, midpoint), Color.White.WithAlpha(0.5f));
            }
        }
        finally
        {
            handle.SetTransform(oldTransform);
            handle.UseShader(oldShader);
        }
    }

    private bool VisibleEntity(EntityUid uid) => _metadata.TryComp(uid, out var metadata) &&
        metadata.EntityLifeStage < EntityLifeStage.Terminating && (metadata.Flags & MetaDataFlags.Detached) == 0;
}
