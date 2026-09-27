using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Provides native tools without creating a cult or granting antagonist membership.</summary>
public sealed partial class OrbitraRatvarCogscarabSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarCogscarabComponent, MapInitEvent>(OnInit, after: [typeof(SharedHandsSystem)]);
        SubscribeLocalEvent<OrbitraRatvarCogscarabComponent, ShotAttemptedEvent>(OnShoot);
    }

    private void OnInit(Entity<OrbitraRatvarCogscarabComponent> ent, ref MapInitEvent args)
    {
        foreach (var prototype in ent.Comp.Tools)
        {
            var tool = Spawn(prototype, Transform(ent).Coordinates);
            _hands.TryPickupAnyHand(ent.Owner, tool);
        }
    }

    private void OnShoot(Entity<OrbitraRatvarCogscarabComponent> ent, ref ShotAttemptedEvent args) => args.Cancel();
}
