using System.Numerics;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Server-authoritative, locally queried suppression paid from the owning cult's energy.</summary>
public sealed partial class OrbitraRatvarLensSystem : EntitySystem
{
    [Dependency] private OrbitraRatvarPowerSystem _power = default!;
    [Dependency] private OrbitraRatvarRuleSystem _rule = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private MovementModStatusSystem _movement = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedEmpSystem _emp = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly EntProtoId SlowStatus = "OrbitraRatvarInterdictionStatus";
    private readonly HashSet<Entity<MobStateComponent>> _mobs = [];
    private readonly HashSet<Entity<MechComponent>> _mechs = [];
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarLensComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<OrbitraRatvarLensComponent, AnchorStateChangedEvent>(OnAnchor);
        SubscribeLocalEvent<OrbitraRatvarLensComponent, ExaminedEvent>(OnExamine);
    }

    private void OnInteract(Entity<OrbitraRatvarLensComponent> ent, ref InteractHandEvent args)
    {
        if (!args.Handled) args.Handled = TryToggle(ent, args.User);
    }

    private void OnAnchor(Entity<OrbitraRatvarLensComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored) RemComp<ActiveOrbitraRatvarLensComponent>(ent);
        UpdateAppearance(ent);
    }

    private void OnExamine(Entity<OrbitraRatvarLensComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("orbitra-ratvar-lens-status",
                ("enabled", HasComp<ActiveOrbitraRatvarLensComponent>(ent))));
    }

    /// <summary>Only a nearby member of the owning cult can enable or disable the lens.</summary>
    public bool TryToggle(Entity<OrbitraRatvarLensComponent> ent, EntityUid user)
    {
        if (!CanToggle(ent, user)) return false;
        if (HasComp<ActiveOrbitraRatvarLensComponent>(ent)) RemComp<ActiveOrbitraRatvarLensComponent>(ent);
        else EnsureComp<ActiveOrbitraRatvarLensComponent>(ent);
        ent.Comp.NextPulse = _timing.CurTime + ent.Comp.Interval;
        UpdateAppearance(ent);
        return true;
    }

    /// <summary>Runs one scan, revalidating power before every target and charging exactly once per effect.</summary>
    public bool TryPulse(Entity<OrbitraRatvarLensComponent> ent)
    {
        if (!CanPulse(ent, out var powered)) return false;
        ent.Comp.NextPulse = _timing.CurTime + ent.Comp.Interval;
        var affected = false;
        var coordinates = Transform(ent).Coordinates;
        _mobs.Clear();
        _lookup.GetEntitiesInRange(coordinates, ent.Comp.Radius, _mobs);
        foreach (var target in _mobs)
        {
            if (target.Comp.CurrentState == MobState.Dead || _rule.TryGetCult(target, out _) ||
                HasComp<MechComponent>(target) || !CanReach(ent, target)) continue;
            if (!_power.TryUsePower((ent, powered))) break;
            _movement.TryUpdateMovementSpeedModDuration(target, SlowStatus,
                ent.Comp.SlowDuration, ent.Comp.SpeedMultiplier);
            affected = true;
        }
        _mechs.Clear();
        _lookup.GetEntitiesInRange(coordinates, ent.Comp.Radius, _mechs);
        foreach (var target in _mechs)
        {
            // Bee воздействует на мехов независимо от пилота; обычные роботы получают только замедление.
            if (target.Comp.Broken || !CanReach(ent, target)) continue;
            if (!_power.TryUsePower((ent, powered))) break;
            _emp.TryEmpEffects(target, ent.Comp.EmpDrain, ent.Comp.EmpDuration, ent);
            if (target.Comp.BatterySlot.ContainedEntity is { } battery)
                _emp.TryEmpEffects(battery, ent.Comp.EmpDrain, ent.Comp.EmpDuration, ent);
            _damageable.TryChangeDamage(target.Owner, ent.Comp.MechDamage, origin: ent);
            affected = true;
        }
        return affected;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate) return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        var query = EntityQueryEnumerator<ActiveOrbitraRatvarLensComponent, OrbitraRatvarLensComponent>();
        while (query.MoveNext(out var uid, out _, out var lens))
        {
            TryPulse((uid, lens));
            UpdateAppearance((uid, lens));
        }
    }

    private bool CanToggle(Entity<OrbitraRatvarLensComponent> ent, EntityUid user)
    {
        if (TerminatingOrDeleted(ent) || EntityManager.IsQueuedForDeletion(ent) || !Transform(ent).Anchored ||
            _container.IsEntityInContainer(ent) || !_rule.TryGetCult(user, out var cult) ||
            !TryComp<OrbitraRatvarStructureComponent>(ent, out var structure) || structure.Rule != cult.Owner ||
            !_actionBlocker.CanInteract(user, ent) || !_interaction.InRangeUnobstructed(user, ent.Owner)) return false;
        return HasComp<ActiveOrbitraRatvarLensComponent>(ent) ||
               TryComp<OrbitraRatvarPoweredComponent>(ent, out var powered) &&
               _power.CanUsePower((ent, powered), out _, out _);
    }

    private bool CanPulse(Entity<OrbitraRatvarLensComponent> ent, out OrbitraRatvarPoweredComponent powered)
    {
        powered = default!;
        if (!TryComp<OrbitraRatvarPoweredComponent>(ent, out var component)) return false;
        powered = component;
        return HasComp<ActiveOrbitraRatvarLensComponent>(ent) && ent.Comp.Interval > TimeSpan.Zero &&
               float.IsFinite(ent.Comp.Radius) && ent.Comp.Radius > 0 &&
               _timing.CurTime >= ent.Comp.NextPulse && _power.CanUsePower((ent, powered), out _, out _);
    }

    private bool CanReach(Entity<OrbitraRatvarLensComponent> ent, EntityUid target)
    {
        if (TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target) ||
            _container.IsEntityInContainer(target)) return false;
        var origin = Transform(ent);
        var destination = Transform(target);
        return destination.GridUid == origin.GridUid && destination.MapID == origin.MapID &&
               Vector2.DistanceSquared(_transform.GetWorldPosition(origin), _transform.GetWorldPosition(destination)) <=
               ent.Comp.Radius * ent.Comp.Radius &&
               _interaction.InRangeUnobstructed(ent.Owner, target, range: ent.Comp.Radius);
    }

    private void UpdateAppearance(Entity<OrbitraRatvarLensComponent> ent)
    {
        var state = !Transform(ent).Anchored ? "Unanchored" :
            HasComp<ActiveOrbitraRatvarLensComponent>(ent) && TryComp<OrbitraRatvarPoweredComponent>(ent, out var powered) &&
            _power.CanUsePower((ent, powered), out _, out _) ? "Active" : "Inactive";
        _appearance.SetData(ent, OrbitraRatvarVisuals.Lens, state);
    }
}
