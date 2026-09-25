using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics.Events;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarRuleSystem
{
    [Dependency] private MovementModStatusSystem _movement = default!;

    private void InitializeDefences()
    {
        SubscribeLocalEvent<OrbitraRatvarStructureComponent, StartCollideEvent>(OnSigilContact);
    }

    private void OnSigilContact(Entity<OrbitraRatvarStructureComponent> ent, ref StartCollideEvent args)
    {
        if (!ent.Comp.Slowing || !Transform(ent).Anchored || !Living(args.OtherEntity) ||
            TryGetCult(args.OtherEntity, out _)) return;
        _movement.TryUpdateMovementSpeedModDuration(args.OtherEntity, "OrbitraRatvarSlowStatus",
            ent.Comp.SlowDuration, ent.Comp.SlowMultiplier);
    }
}
