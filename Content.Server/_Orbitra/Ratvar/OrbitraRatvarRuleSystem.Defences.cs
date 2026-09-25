using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics.Events;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarRuleSystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MovementModStatusSystem _movement = default!;

    private void InitializeDefences()
    {
        SubscribeLocalEvent<OrbitraRatvarStructureComponent, MapInitEvent>(OnDefenceInit);
        SubscribeLocalEvent<OrbitraRatvarStructureComponent, OrbitraRatvarDefenceEvent>(OnDefence);
        SubscribeLocalEvent<OrbitraRatvarStructureComponent, DamageModifyEvent>(OnDefenceDamage);
        SubscribeLocalEvent<OrbitraRatvarStructureComponent, StartCollideEvent>(OnSigilContact);
    }

    private void OnDefenceInit(Entity<OrbitraRatvarStructureComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Marauder) _actions.AddAction(ent.Owner, ref ent.Comp.DefenceAction, "OrbitraActionRatvarDefence");
    }

    private void OnDefence(Entity<OrbitraRatvarStructureComponent> ent, ref OrbitraRatvarDefenceEvent args)
    {
        if (!args.Handled) args.Handled = TryDefend(ent);
    }

    /// <summary>Begins a finite damage-reduction stance; the action controls its cooldown.</summary>
    public bool TryDefend(Entity<OrbitraRatvarStructureComponent> ent)
    {
        if (!CanDefend(ent)) return false;
        ent.Comp.DefenceUntil = Timing.CurTime + ent.Comp.DefenceDuration;
        return true;
    }

    public bool CanDefend(Entity<OrbitraRatvarStructureComponent> ent) => ent.Comp.Marauder &&
        Living(ent) && TryGetCult(ent, out _) && ent.Comp.DefenceUntil <= Timing.CurTime;

    private void OnDefenceDamage(Entity<OrbitraRatvarStructureComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.Marauder && ent.Comp.DefenceUntil > Timing.CurTime && args.Damage.AnyPositive())
            args.Damage *= ent.Comp.DefenceMultiplier;
    }

    private void OnSigilContact(Entity<OrbitraRatvarStructureComponent> ent, ref StartCollideEvent args)
    {
        if (!ent.Comp.Slowing || !Transform(ent).Anchored || !Living(args.OtherEntity) ||
            TryGetCult(args.OtherEntity, out _)) return;
        _movement.TryUpdateMovementSpeedModDuration(args.OtherEntity, "OrbitraRatvarSlowStatus",
            ent.Comp.SlowDuration, ent.Comp.SlowMultiplier);
    }
}
