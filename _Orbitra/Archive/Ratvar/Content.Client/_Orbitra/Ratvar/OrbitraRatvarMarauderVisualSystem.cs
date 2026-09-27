using Content.Shared._Orbitra.Ratvar;
using Robust.Client.GameObjects;

namespace Content.Client._Orbitra.Ratvar;

/// <summary>Freezes only the shield layer, preserving the construct's normal directional animation.</summary>
public sealed partial class OrbitraRatvarMarauderVisualSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarMarauderVisualComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<OrbitraRatvarMarauderVisualComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite)) return;
        _sprite.LayerSetAutoAnimated((ent, sprite), OrbitraRatvarMarauderLayers.Shield, false);
        _sprite.LayerSetAnimationTime((ent, sprite), OrbitraRatvarMarauderLayers.Shield, 0);
    }
}
