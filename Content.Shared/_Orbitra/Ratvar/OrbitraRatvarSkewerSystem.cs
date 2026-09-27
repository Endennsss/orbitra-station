using Content.Shared.Buckle.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>Applies the same occupant collision exclusion on server and predicting client.</summary>
public sealed class OrbitraRatvarSkewerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarSkewerComponent, PreventCollideEvent>(OnCollide);
    }

    private void OnCollide(Entity<OrbitraRatvarSkewerComponent> ent, ref PreventCollideEvent args)
    {
        if (TryComp<BuckleComponent>(args.OtherEntity, out var buckle) && buckle.BuckledTo == ent.Owner)
            args.Cancelled = true;
    }
}
