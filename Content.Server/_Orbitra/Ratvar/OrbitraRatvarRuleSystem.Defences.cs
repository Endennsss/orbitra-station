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
        TryTriggerSlowingSigil(ent, args.OtherEntity);
    }

    /// <summary>Applies the slowing sigil only while its own cult and contact remain valid.</summary>
    public bool TryTriggerSlowingSigil(Entity<OrbitraRatvarStructureComponent> ent, EntityUid target)
    {
        if (!CanTriggerSlowingSigil(ent, target))
            return false;
        _movement.TryUpdateMovementSpeedModDuration(target, "OrbitraRatvarSlowStatus",
            ent.Comp.SlowDuration, ent.Comp.SlowMultiplier);
        return true;
    }

    private bool CanTriggerSlowingSigil(Entity<OrbitraRatvarStructureComponent> ent, EntityUid target)
    {
        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(target) || !ent.Comp.Slowing ||
            ent.Comp.Rule is not { } owner || !TryComp<OrbitraRatvarRuleComponent>(owner, out var cult) ||
            cult.Won || cult.Lost || !GameTicker.IsGameRuleActive(owner) || !Transform(ent).Anchored ||
            !Living(target) || _containers.IsEntityInContainer(ent) || _containers.IsEntityInContainer(target) ||
            !Near(ent, target, 1f))
            return false;
        return !TryGetCult(target, out _);
    }
}
