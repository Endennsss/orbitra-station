using Content.Shared._Orbitra.Particles;
using Content.Server.Power.Components;
using Content.Shared.Wall;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.Destructible;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._Orbitra.Particles;

/// <summary>Publishes confirmed transient effects to nearby observers.</summary>
public sealed partial class OrbitraParticleBurstSystem : EntitySystem
{
    [Dependency] private SharedOrbitraParticleSystem _particles = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly HashSet<EntityUid> _electricalBursts = new();
    private TimeSpan _burstTime;

    public override void Initialize()
    {
        InitializeDust();
        SubscribeLocalEvent<WallComponent, OrbitraParticleMeleeHitEvent>(OnHit);
        SubscribeLocalEvent<OrbitraParticleMaterialComponent, OrbitraParticleMeleeHitEvent>(OnMaterialHit);
        SubscribeLocalEvent<ApcPowerReceiverComponent, OrbitraParticleBeforeBreakEvent>(OnBreak);
        SubscribeLocalEvent<ApcPowerReceiverComponent, DamageThresholdReached>(OnThreshold);
        SubscribeLocalEvent<BloodstreamComponent, OrbitraParticleMeleeHitEvent>(OnBloodHit);
    }

    private void OnThreshold(Entity<ApcPowerReceiverComponent> ent, ref DamageThresholdReached args)
    {
        var replacement = false;
        foreach (var behavior in args.Threshold.Behaviors)
        {
            // BreakEntity уже публикует свой эффект; не создаём второй выброс.
            if (behavior is DoActsBehavior acts && acts.HasAct(ThresholdActs.Breakage))
                return;
            replacement |= behavior is ChangeConstructionNodeBehavior ||
                behavior is DoActsBehavior destruction && destruction.HasAct(ThresholdActs.Destruction);
        }
        if (replacement)
            TryBreak(ent, ent.Comp);
    }

    private void OnHit(EntityUid uid, WallComponent component, ref OrbitraParticleMeleeHitEvent args)
    {
        if (!HasComp<OrbitraParticleMaterialComponent>(uid))
            TryHit(uid, args);
    }

    private void OnMaterialHit(EntityUid uid, OrbitraParticleMaterialComponent component, ref OrbitraParticleMeleeHitEvent args)
    {
        if (!HasComp<BloodstreamComponent>(uid))
            TryHit(uid, args);
    }

    private void OnBloodHit(Entity<BloodstreamComponent> ent, ref OrbitraParticleMeleeHitEvent args)
    {
        TryBloodHit(ent, args.User, args.Damage);
    }

    /// <summary>Emits cosmetic blood only for confirmed physical damage to a blood-filled body.</summary>
    public bool TryBloodHit(EntityUid target, EntityUid attacker, DamageSpecifier damage)
    {
        if (!CanBloodHit(target, attacker, damage, out var blood))
            return false;
        var point = _particles.Contact(target, attacker, out var direction);
        var reference = blood.BloodReferenceSolution;
        var tint = reference.GetColor(_prototype);
        tint.A = 1f;
        Send(target, point, "OrbitraParticleBlood", direction, tint);
        return true;
    }

    private bool CanBloodHit(EntityUid target, EntityUid attacker, DamageSpecifier damage,
        [NotNullWhen(true)] out BloodstreamComponent? blood)
    {
        blood = null;
        return CanHit(target, new OrbitraParticleMeleeHitEvent(attacker, damage)) &&
            TryComp(target, out blood) && blood.BloodSolution is { } solution &&
            solution.Comp.Solution.Volume > 0 && !_container.IsEntityInContainer(target);
    }

    private bool TryHit(EntityUid uid, OrbitraParticleMeleeHitEvent args)
    {
        if (!CanHit(uid, args))
            return false;
        var point = _particles.Contact(uid, args.User, out var direction);
        Send(uid, point, _particles.MaterialEffect(uid), direction);
        return true;
    }

    private bool CanHit(EntityUid uid, OrbitraParticleMeleeHitEvent args)
    {
        if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.User) || Transform(uid).MapUid == null)
            return false;
        foreach (var (type, amount) in args.Damage.DamageDict)
        {
            if (type.Id is "Blunt" or "Slash" or "Piercing" && amount > 0)
                return true;
        }
        return false;
    }

    private void OnBreak(EntityUid uid, ApcPowerReceiverComponent component, ref OrbitraParticleBeforeBreakEvent args)
    {
        TryBreak(uid, component);
    }

    private bool CanBreak(EntityUid uid, ApcPowerReceiverComponent component) =>
        component.Powered && component.NeedsPower && !component.PowerDisabled && component.PowerReceived > 0 &&
        Transform(uid).MapUid != null && !_container.IsEntityInContainer(uid);

    private bool TryBreak(EntityUid uid, ApcPowerReceiverComponent component)
    {
        if (!CanBreak(uid, component))
            return false;
        // Несколько порогов одного удара могут включать и замену корпуса, и Breakage.
        if (_burstTime != _timing.CurTime)
        {
            _burstTime = _timing.CurTime;
            _electricalBursts.Clear();
        }
        if (!_electricalBursts.Add(uid))
            return false;
        var xform = Transform(uid);
        var point = _transform.ToCoordinates(xform.GridUid ?? xform.MapUid!.Value, _transform.GetMapCoordinates(uid));
        Send(uid, point, "OrbitraParticleElectrical", 0);
        return true;
    }

    private void Send(EntityUid source, EntityCoordinates coordinates, string effect, float direction, Color? tint = null)
    {
        RaiseNetworkEvent(new OrbitraParticleBurstEvent(GetNetEntity(source), GetNetCoordinates(coordinates), effect, direction, tint),
            Filter.Pvs(coordinates, entityMan: EntityManager));
    }
}
