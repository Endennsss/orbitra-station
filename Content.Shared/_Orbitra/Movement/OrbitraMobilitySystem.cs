using System.Numerics;
using Content.Shared._Orbitra.Input;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Orbitra.Movement;

public sealed partial class OrbitraMobilitySystem : VirtualController
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(SharedMoverController));
        base.Initialize();

        SubscribeLocalEvent<OrbitraProneComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshProneSpeed);
        SubscribeLocalEvent<OrbitraProneComponent, ComponentShutdown>(OnProneShutdown);
        SubscribeLocalEvent<OrbitraActiveManeuverComponent, UpdateCanMoveEvent>(OnManeuverCanMove);
        SubscribeLocalEvent<OrbitraMobilityComponent, KnockedDownEvent>(OnKnockedDown);
        SubscribeLocalEvent<OrbitraProneComponent, OrbitraStandDoAfterEvent>(OnStandDoAfter);

        CommandBinds.Builder
            .Bind(OrbitraKeyFunctions.ToggleProne, InputCmdHandler.FromDelegate(OnProneInput, handle: false))
            .Bind(OrbitraKeyFunctions.Jump, InputCmdHandler.FromDelegate(OnJumpInput, handle: false))
            .Register<OrbitraMobilitySystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<OrbitraMobilitySystem>();
        base.Shutdown();
    }

    private void OnProneInput(ICommonSession? session)
    {
        if (session?.AttachedEntity is { Valid: true } uid)
            TryToggleProne(uid);
    }

    private void OnJumpInput(ICommonSession? session)
    {
        if (session?.AttachedEntity is { Valid: true } uid)
            TryJump(uid);
    }

    public bool TryToggleProne(Entity<OrbitraMobilityComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        if (TryComp<OrbitraProneComponent>(entity, out var prone))
        {
            if (prone.IsStandingUp)
            {
                _doAfter.Cancel(prone.StandDoAfter);
                return true;
            }

            return TryStand(entity);
        }

        if (TryGetDirection(entity, out var direction))
            return TryRoll((entity, entity.Comp), direction);

        if (!CanGoProne(entity, false))
            return false;

        DoGoProne(entity);
        return true;
    }

    public bool CanGoProne(Entity<OrbitraMobilityComponent?> entity, bool quiet = true)
    {
        if (!Resolve(entity, ref entity.Comp, false) || !CanManeuver(entity, quiet))
            return false;

        return !HasComp<OrbitraProneComponent>(entity) && !_standing.IsDown(entity.Owner);
    }

    private void DoGoProne(EntityUid uid)
    {
        if (!_standing.Down(uid, dropHeldItems: false))
            return;

        EnsureComp<OrbitraProneComponent>(uid);
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    public bool TryStand(Entity<OrbitraMobilityComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false) || !TryComp<OrbitraProneComponent>(entity, out var prone) ||
            !CanStand(entity, prone, false))
            return false;

        var args = new DoAfterArgs(EntityManager, entity, entity.Comp.StandDuration,
            new OrbitraStandDoAfterEvent(), entity, target: entity)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            RequireCanInteract = false,
        };
        if (!_doAfter.TryStartDoAfter(args, out var id))
            return false;

        prone.StandDoAfter = id;
        prone.IsStandingUp = true;
        Dirty(entity.Owner, prone);
        return true;
    }

    public bool CanStand(Entity<OrbitraMobilityComponent?> entity, OrbitraProneComponent prone, bool quiet = true)
    {
        if (!Resolve(entity, ref entity.Comp, false) || prone.IsStandingUp || HasComp<KnockedDownComponent>(entity) ||
            HasComp<OrbitraActiveManeuverComponent>(entity))
            return false;

        if (_physics.GetEntitiesIntersectingBody(entity.Owner, StandingStateSystem.StandingCollisionLayer, false).Count > 0)
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("orbitra-mobility-cannot-stand"), entity.Owner, entity.Owner);
            return false;
        }
        return true;
    }

    private void OnStandDoAfter(Entity<OrbitraProneComponent> entity, ref OrbitraStandDoAfterEvent args)
    {
        entity.Comp.StandDoAfter = null;
        entity.Comp.IsStandingUp = false;
        Dirty(entity);
        if (args.Cancelled || !TryComp<OrbitraMobilityComponent>(entity, out var mobility) ||
            !CanStand((entity.Owner, mobility), entity.Comp, false) || !_standing.Stand(entity))
            return;

        RemComp<OrbitraProneComponent>(entity);
    }

    public bool TryRoll(Entity<OrbitraMobilityComponent?> entity, Vector2 direction)
    {
        if (!Resolve(entity, ref entity.Comp, false) || !CanRoll((entity.Owner, entity.Comp), direction, false))
            return false;

        DoGoProne(entity.Owner);
        DoManeuver((entity.Owner, entity.Comp), OrbitraManeuverType.Roll, direction,
            entity.Comp.RollDistance, entity.Comp.RollDuration, entity.Comp.RollStaminaCost);
        entity.Comp.NextRoll = _timing.CurTime + TimeSpan.FromSeconds(entity.Comp.RollCooldown);
        Dirty(entity);
        return true;
    }

    public bool CanRoll(Entity<OrbitraMobilityComponent?> entity, Vector2 direction, bool quiet = true)
    {
        if (!Resolve(entity, ref entity.Comp, false) || direction == Vector2.Zero || !CanManeuver(entity, quiet))
            return false;

        if (_timing.CurTime < entity.Comp.NextRoll || HasComp<OrbitraProneComponent>(entity))
            return false;

        var ev = new OrbitraRollAttemptEvent(entity.Comp.RollStaminaCost);
        RaiseLocalEvent(entity.Owner, ref ev);
        return !ev.Cancelled && CanPayStamina(entity.Owner, ev.StaminaCost, quiet);
    }

    public bool TryJump(Entity<OrbitraMobilityComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        TryGetDirection(entity, out var direction);
        if (!CanJump((entity.Owner, entity.Comp), direction, false))
            return false;

        if (direction != Vector2.Zero && TryFindVaultTarget(entity.Owner, direction, entity.Comp.JumpDistance, out var target) &&
            _climb.TryOrbitraJumpVault(entity.Owner, target))
        {
            _stamina.TryTakeStamina(entity.Owner, entity.Comp.JumpStaminaCost, visual: false);
        }
        else
        {
            DoManeuver((entity.Owner, entity.Comp), OrbitraManeuverType.Jump, direction,
                entity.Comp.JumpDistance, entity.Comp.JumpDuration, entity.Comp.JumpStaminaCost);
        }
        entity.Comp.NextJump = _timing.CurTime + TimeSpan.FromSeconds(entity.Comp.JumpCooldown);
        Dirty(entity);
        return true;
    }

    public bool CanJump(Entity<OrbitraMobilityComponent?> entity, Vector2 direction, bool quiet = true)
    {
        if (!Resolve(entity, ref entity.Comp, false) || !CanManeuver(entity, quiet))
            return false;

        if (_timing.CurTime < entity.Comp.NextJump || _standing.IsDown(entity.Owner))
            return false;

        var ev = new OrbitraJumpAttemptEvent(entity.Comp.JumpStaminaCost);
        RaiseLocalEvent(entity.Owner, ref ev);
        return !ev.Cancelled && CanPayStamina(entity.Owner, ev.StaminaCost, quiet);
    }

    private bool CanManeuver(EntityUid uid, bool quiet)
    {
        var valid = !HasComp<OrbitraActiveManeuverComponent>(uid) &&
                    !HasComp<KnockedDownComponent>(uid) &&
                    !HasComp<StunnedComponent>(uid) &&
                    !HasComp<ThrownItemComponent>(uid) &&
                    !_containers.IsEntityInContainer(uid) &&
                    !_gravity.IsWeightless(uid) &&
                    _blocker.CanMove(uid) &&
                    _mobState.IsAlive(uid) &&
                    (!TryComp<BuckleComponent>(uid, out var buckle) || !buckle.Buckled) &&
                    (!TryComp<ClimbingComponent>(uid, out var climbing) || !climbing.IsClimbing);

        if (!valid && !quiet)
            _popup.PopupEntity(Loc.GetString("orbitra-mobility-cannot-move"), uid, uid);
        return valid;
    }

    private bool CanPayStamina(EntityUid uid, float cost, bool quiet)
    {
        if (!TryComp<StaminaComponent>(uid, out var stamina) ||
            !stamina.Critical && stamina.StaminaDamage + cost < stamina.CritThreshold)
            return true;

        if (!quiet)
            _popup.PopupEntity(Loc.GetString("orbitra-mobility-no-stamina"), uid, uid);
        return false;
    }

    private void DoManeuver(Entity<OrbitraMobilityComponent> entity, OrbitraManeuverType type, Vector2 direction,
        float distance, float duration, float staminaCost)
    {
        _stamina.TryTakeStamina(entity.Owner, staminaCost, visual: false);
        var active = EnsureComp<OrbitraActiveManeuverComponent>(entity.Owner);
        active.Type = type;
        active.Direction = direction.Normalized();
        active.Speed = distance / duration;
        active.StartTime = _timing.CurTime;
        active.EndTime = _timing.CurTime + TimeSpan.FromSeconds(duration);
        Dirty(entity.Owner, active);
        _blocker.UpdateCanMove(entity.Owner);

        if (type == OrbitraManeuverType.Jump && TryComp<PhysicsComponent>(entity, out var body))
            _physics.SetBodyStatus(entity.Owner, body, BodyStatus.InAir);
    }

    private bool TryGetDirection(EntityUid uid, out Vector2 direction)
    {
        direction = Vector2.Zero;
        if (!TryComp<InputMoverComponent>(uid, out var input) || !input.HasDirectionalMovement)
            return false;

        direction = _mover.GetWishDir((uid, input));
        return direction != Vector2.Zero;
    }

    private bool TryFindVaultTarget(EntityUid uid, Vector2 direction, float range, out EntityUid target)
    {
        target = default;
        var origin = _transform.GetWorldPosition(uid);
        var bestDistance = float.MaxValue;

        foreach (var candidate in _lookup.GetEntitiesInRange(uid, range, LookupFlags.Static | LookupFlags.Dynamic))
        {
            if (candidate == uid || !TryComp<ClimbableComponent>(candidate, out var climbable) ||
                !_climb.CanVault(climbable, uid, candidate, out _))
                continue;

            var delta = _transform.GetWorldPosition(candidate) - origin;
            var distance = delta.Length();
            if (distance <= 0.05f || distance >= bestDistance || Vector2.Dot(delta / distance, direction) < 0.75f)
                continue;

            bestDistance = distance;
            target = candidate;
        }

        return target.IsValid();
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);
        var query = EntityQueryEnumerator<OrbitraActiveManeuverComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var maneuver, out var body))
        {
            if (_timing.CurTime >= maneuver.EndTime)
            {
                FinishManeuver((uid, maneuver), body);
                continue;
            }

            var speed = maneuver.Speed;
            if (maneuver.Type == OrbitraManeuverType.Roll && TryComp<OrbitraMobilityComponent>(uid, out var mobility))
            {
                var duration = Math.Max(mobility.RollDuration, 0.01f);
                var progress = Math.Clamp((float) (_timing.CurTime - maneuver.StartTime).TotalSeconds / duration, 0f, 1f);
                speed *= mobility.RollDeceleration * MathF.Pow(1f - progress, mobility.RollDeceleration - 1f);
            }
            _physics.SetLinearVelocity(uid, maneuver.Direction * speed, body: body);
        }
    }

    private void FinishManeuver(Entity<OrbitraActiveManeuverComponent> entity, PhysicsComponent body)
    {
        _physics.SetLinearVelocity(entity.Owner, Vector2.Zero, body: body);
        if (entity.Comp.Type == OrbitraManeuverType.Jump)
            _physics.SetBodyStatus(entity.Owner, body, BodyStatus.OnGround);

        RemComp<OrbitraActiveManeuverComponent>(entity.Owner);
        _blocker.UpdateCanMove(entity.Owner);
    }

    private void OnRefreshProneSpeed(Entity<OrbitraProneComponent> entity, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (TryComp<OrbitraMobilityComponent>(entity, out var mobility))
            args.ModifySpeed(mobility.CrawlSpeedModifier);
    }

    private void OnProneShutdown(Entity<OrbitraProneComponent> entity, ref ComponentShutdown args)
    {
        _movement.RefreshMovementSpeedModifiers(entity.Owner);
    }

    private void OnManeuverCanMove(Entity<OrbitraActiveManeuverComponent> entity, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnKnockedDown(Entity<OrbitraMobilityComponent> entity, ref KnockedDownEvent args)
    {
        RemComp<OrbitraProneComponent>(entity.Owner);
    }
}
