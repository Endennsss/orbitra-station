using System.Numerics;
using Content.Shared._Orbitra.Movement;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;
using Depth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Orbitra.Movement;

public sealed partial class OrbitraMobilityVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprites = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraActiveManeuverComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<OrbitraActiveManeuverComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OrbitraProneComponent, ComponentStartup>(OnProneStartup);
        SubscribeLocalEvent<OrbitraProneComponent, ComponentRemove>(OnProneRemove);
    }

    private void OnStartup(Entity<OrbitraActiveManeuverComponent> entity, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;

        var visuals = EnsureComp<OrbitraMobilityVisualsComponent>(entity);
        RemoveJumpOffset((entity.Owner, visuals), sprite);
    }

    private void OnShutdown(Entity<OrbitraActiveManeuverComponent> entity, ref ComponentShutdown args)
    {
        if (!TryComp<OrbitraMobilityVisualsComponent>(entity, out var visuals) ||
            !TryComp<SpriteComponent>(entity, out var sprite))
            return;

        RemoveJumpOffset((entity.Owner, visuals), sprite);
        RemComp<OrbitraMobilityVisualsComponent>(entity.Owner);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var query = EntityQueryEnumerator<OrbitraActiveManeuverComponent, OrbitraMobilityComponent,
            OrbitraMobilityVisualsComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var maneuver, out var mobility, out var visuals, out var sprite))
        {
            var duration = maneuver.Type == OrbitraManeuverType.Roll ? mobility.RollDuration : mobility.JumpDuration;
            var remaining = (float) (maneuver.EndTime - _timing.CurTime).TotalSeconds;
            var progress = Math.Clamp(1f - remaining / duration, 0f, 1f);

            if (maneuver.Type == OrbitraManeuverType.Jump)
            {
                var height = MathF.Sin(progress * MathF.PI) * 0.3f;
                var offset = sprite.Offset - visuals.AppliedOffset + new Vector2(0f, height);
                visuals.AppliedOffset = new Vector2(0f, height);
                _sprites.SetOffset((uid, sprite), offset);
            }
            else
            {
                RemoveJumpOffset((uid, visuals), sprite);
            }
        }
    }

    private void RemoveJumpOffset(Entity<OrbitraMobilityVisualsComponent> entity, SpriteComponent sprite)
    {
        if (entity.Comp.AppliedOffset == Vector2.Zero)
            return;

        _sprites.SetOffset((entity.Owner, sprite), sprite.Offset - entity.Comp.AppliedOffset);
        entity.Comp.AppliedOffset = Vector2.Zero;
    }

    private void OnProneSpriteStartup(Entity<SpriteComponent> entity)
    {
        var visuals = EnsureComp<OrbitraProneVisualsComponent>(entity);
        visuals.BaseDrawDepth = entity.Comp.DrawDepth;
        _sprites.SetDrawDepth(entity.AsNullable(), (int) Depth.SmallMobs);
    }

    private void OnProneStartup(Entity<OrbitraProneComponent> entity, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;
        OnProneSpriteStartup((entity.Owner, sprite));
    }

    private void OnProneRemove(Entity<OrbitraProneComponent> entity, ref ComponentRemove args)
    {
        if (!TryComp<OrbitraProneVisualsComponent>(entity, out var visuals) ||
            !TryComp<SpriteComponent>(entity, out var sprite))
            return;
        _sprites.SetDrawDepth((entity.Owner, sprite), visuals.BaseDrawDepth);
        RemComp<OrbitraProneVisualsComponent>(entity);
    }
}

[RegisterComponent]
public sealed partial class OrbitraMobilityVisualsComponent : Component
{
    public Vector2 AppliedOffset;
}

[RegisterComponent]
public sealed partial class OrbitraProneVisualsComponent : Component
{
    public int BaseDrawDepth;
}
