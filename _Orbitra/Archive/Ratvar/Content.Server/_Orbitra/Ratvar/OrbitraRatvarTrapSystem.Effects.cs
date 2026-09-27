using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Mobs.Components;
using Content.Shared.Throwing;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarTrapSystem
{
    // Воздействие ловушек использует штатные броски, урон и удержание тела.
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private BloodstreamSystem _blood = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    private void InitializeEffects()
    {
        SubscribeLocalEvent<OrbitraRatvarTrapComponent, StrapAttemptEvent>(OnStrap);
        SubscribeLocalEvent<OrbitraRatvarTrapComponent, UnstrapAttemptEvent>(OnUnstrap);
        SubscribeLocalEvent<OrbitraRatvarTrapComponent, OrbitraRatvarTrapEscapeEvent>(OnEscape);
        SubscribeLocalEvent<OrbitraRatvarTrapComponent, DoAfterAttemptEvent<OrbitraRatvarTrapEscapeEvent>>(OnEscapeAttempt);
    }

    private bool GroundedLiving(EntityUid target) =>
        HasComp<MobStateComponent>(target) && !_mob.IsDead(target) &&
        !_container.IsEntityInContainer(target) &&
        TryComp<PhysicsComponent>(target, out var physics) && physics.CanCollide &&
        physics.BodyStatus != BodyStatus.InAir && !_gravity.IsWeightless(target);

    private bool SameTile(EntityUid trap, EntityUid target) =>
        Transform(trap).GridUid == Transform(target).GridUid &&
        _transform.TryGetGridTilePosition(trap, out var tile) &&
        _transform.TryGetGridTilePosition(target, out var otherTile) && tile == otherTile;

    private void Impale(Entity<OrbitraRatvarTrapComponent> ent)
    {
        ent.Comp.Extended = true;
        _buckle.StrapSetEnabled(ent, true);
        foreach (var target in _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 1f))
        {
            if (!GroundedLiving(target) || !SameTile(ent, target)) continue;
            var damaged = _damage.TryChangeDamage(target, ent.Comp.Damage, out var dealt, origin: ent);
            // В SS14 кровотечение зависит от прошедшего урона, а не от несовместимой шкалы ран Bee.
            if (damaged && dealt.GetTotal() > 0)
                _blood.TryModifyBleedAmount(target, Math.Min(5f, (float) dealt.GetTotal()));
            if (TryComp<BuckleComponent>(target, out var buckle) && !buckle.Buckled)
                _buckle.TryBuckle(target, null, ent, buckle, popup: false);
        }
        SetSkewerHard(ent, true);
    }

    private void Flip(Entity<OrbitraRatvarTrapComponent> ent)
    {
        var direction = _transform.GetWorldRotation(ent).ToWorldVec() * ent.Comp.ThrowRange;
        foreach (var target in _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 1f))
        {
            if (target == ent.Owner || Transform(target).Anchored || !SameTile(ent, target) ||
                _container.IsEntityInContainer(target) ||
                TryComp<BuckleComponent>(target, out var buckle) && buckle.Buckled) continue;
            _throwing.TryThrow(target, direction, ent.Comp.ThrowSpeed,
                compensateFriction: true, recoil: false, doSpin: false);
        }
    }

    private void Retract(Entity<OrbitraRatvarTrapComponent> ent)
    {
        ent.Comp.Extended = false;
        if (ent.Comp.Kind == OrbitraRatvarTrapKind.Skewer)
        {
            SetSkewerHard(ent, false);
            _buckle.StrapSetEnabled(ent, false);
        }
        if (TryComp<ActiveOrbitraRatvarTrapComponent>(ent, out var active)) active.Pulse = null;
        RemCompDeferred<ActiveOrbitraRatvarTrapComponent>(ent);
        _appearance.SetData(ent, OrbitraRatvarVisuals.Trap, false);
    }

    private void SetSkewerHard(EntityUid uid, bool hard)
    {
        if (TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.Fixtures.TryGetValue("fix1", out var fixture))
        {
            _physics.SetHard(uid, fixture, hard, fixtures);
            _physics.RegenerateContacts(uid);
        }
    }

    private void OnStrap(Entity<OrbitraRatvarTrapComponent> ent, ref StrapAttemptEvent args)
    {
        // Ловушка не является обычным креслом: только собственное срабатывание удерживает цель.
        if (!ent.Comp.Extended || args.User != null) args.Cancelled = true;
    }

    private void OnUnstrap(Entity<OrbitraRatvarTrapComponent> ent, ref UnstrapAttemptEvent args)
    {
        if (!ent.Comp.Extended || args.User is not { } user) return;
        args.Cancelled = true;
        TryEscape(ent, user, args.Buckle);
    }

    /// <summary>Starts a five-second escape without charging energy or requiring cult membership.</summary>
    public bool TryEscape(Entity<OrbitraRatvarTrapComponent> ent, EntityUid user, EntityUid victim)
    {
        if (!CanEscape(ent, user, victim)) return false;
        var args = new DoAfterArgs(EntityManager, user, ent.Comp.EscapeTime,
            new OrbitraRatvarTrapEscapeEvent { Victim = GetNetEntity(victim) }, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            RequireCanInteract = user != victim,
            AttemptFrequency = AttemptFrequency.EveryTick,
            CancelDuplicate = false,
            BlockDuplicate = true,
        };
        return _doAfter.TryStartDoAfter(args);
    }

    private bool CanEscape(Entity<OrbitraRatvarTrapComponent> ent, EntityUid user, EntityUid victim) =>
        !TerminatingOrDeleted(ent) && ent.Comp.Extended && _mob.IsAlive(user) &&
        TryComp<BuckleComponent>(victim, out var buckle) && buckle.BuckledTo == ent.Owner &&
        _interaction.InRangeUnobstructed(user, ent.Owner) &&
        (user == victim || _blocker.CanInteract(user, ent));

    private void OnEscapeAttempt(Entity<OrbitraRatvarTrapComponent> ent,
        ref DoAfterAttemptEvent<OrbitraRatvarTrapEscapeEvent> args)
    {
        var data = (OrbitraRatvarTrapEscapeEvent) args.DoAfter.Args.Event;
        if (!TryGetEntity(data.Victim, out var victim) || victim == null ||
            !CanEscape(ent, args.DoAfter.Args.User, victim.Value)) args.Cancel();
    }

    private void OnEscape(Entity<OrbitraRatvarTrapComponent> ent, ref OrbitraRatvarTrapEscapeEvent args)
    {
        if (args.Handled || args.Cancelled || !TryGetEntity(args.Victim, out var victim) || victim == null ||
            !CanEscape(ent, args.User, victim.Value)) return;
        args.Handled = true;
        _buckle.Unbuckle(victim.Value, args.User);
    }
}
