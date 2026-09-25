using Content.Shared._Orbitra.Ratvar;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Controls innate marauder defence without creating or requiring an antagonist role.</summary>
public sealed class OrbitraRatvarMarauderSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<OrbitraRatvarMarauderComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<OrbitraRatvarMarauderComponent, OrbitraRatvarDefenceEvent>(OnDefence);
        SubscribeLocalEvent<OrbitraRatvarMarauderComponent, DamageModifyEvent>(OnDamage);
    }

    private void OnInit(Entity<OrbitraRatvarMarauderComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.DefenceAction, "OrbitraActionRatvarDefence");
        _appearance.SetData(ent, OrbitraRatvarVisuals.Defending, false);
    }

    private void OnDefence(Entity<OrbitraRatvarMarauderComponent> ent, ref OrbitraRatvarDefenceEvent args)
    {
        if (!args.Handled && args.Performer == ent.Owner)
            args.Handled = TryDefend(ent);
    }

    private void OnDamage(Entity<OrbitraRatvarMarauderComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.DefenceUntil <= _timing.CurTime)
            return;

        // Лечение не ослабляется вместе с входящим уроном.
        var damage = new DamageSpecifier(args.Damage);
        foreach (var (type, amount) in args.Damage.DamageDict)
        {
            if (amount > 0)
                damage.DamageDict[type] = amount * ent.Comp.DefenceMultiplier;
        }
        args.Damage = damage;
    }

    /// <summary>Starts defence and its authoritative cooldown, including direct server requests.</summary>
    public bool TryDefend(Entity<OrbitraRatvarMarauderComponent> ent)
    {
        if (!CanDefend(ent))
            return false;

        ent.Comp.DefenceUntil = _timing.CurTime + ent.Comp.DefenceDuration;
        ent.Comp.NextDefence = _timing.CurTime + ent.Comp.DefenceCooldown;
        EnsureComp<ActiveOrbitraRatvarDefenceComponent>(ent);
        SetAppearance(ent, true);
        if (ent.Comp.DefenceAction is { } action)
            _actions.SetCooldown(action, ent.Comp.DefenceCooldown);
        return true;
    }

    /// <summary>Requires a conscious controlled body, but no cult or mind role.</summary>
    public bool CanDefend(Entity<OrbitraRatvarMarauderComponent> ent) =>
        HasComp<ActorComponent>(ent) && TryComp<MobStateComponent>(ent, out var mob) &&
        mob.CurrentState == MobState.Alive && _blocker.CanInteract(ent, null) &&
        ent.Comp.DefenceUntil <= _timing.CurTime && ent.Comp.NextDefence <= _timing.CurTime;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ActiveOrbitraRatvarDefenceComponent, OrbitraRatvarMarauderComponent>();
        while (query.MoveNext(out var uid, out _, out var marauder))
        {
            if (marauder.DefenceUntil > _timing.CurTime && HasComp<ActorComponent>(uid) &&
                TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Alive)
                continue;

            marauder.DefenceUntil = TimeSpan.Zero;
            SetAppearance((uid, marauder), false);
            RemCompDeferred<ActiveOrbitraRatvarDefenceComponent>(uid);
        }
    }

    private void SetAppearance(Entity<OrbitraRatvarMarauderComponent> ent, bool active)
    {
        _appearance.SetData(ent, OrbitraRatvarVisuals.Defending, active);
        if (ent.Comp.DefenceAction is { } action)
            _actions.SetToggled(action, active);
    }
}
