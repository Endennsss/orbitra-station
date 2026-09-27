using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Administration.Logs;
using Content.Shared.Administration.Systems;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Station-local biological exchange, independent of the cult's electrical power economy.</summary>
public sealed partial class OrbitraRatvarVitalitySystem : EntitySystem
{
    [Dependency] private OrbitraRatvarRuleSystem _rule = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private RoleSystem _role = default!;
    [Dependency] private JobSystem _job = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private RejuvenateSystem _rejuvenate = default!;
    [Dependency] private SharedRottingSystem _rotting = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private EuiManager _eui = default!;

    private static readonly ProtoId<DamageGroupPrototype> Brute = "Brute";
    private static readonly ProtoId<DamageTypePrototype>[] HealingTypes = ["Heat", "Asphyxiation", "Poison", "Cellular"];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarVitalityComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(Entity<OrbitraRatvarVitalityComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange && _rule.TryGetCult(args.Examiner, out var cult) &&
            TryComp<OrbitraRatvarStructureComponent>(ent, out var structure) && structure.Rule == cult.Owner)
            args.PushMarkup(Loc.GetString("orbitra-ratvar-vitality-stock", ("amount", cult.Comp.Vitality)));
    }

    /// <summary>Starts a fresh charge only for a valid occupant of an owned, anchored sigil.</summary>
    public bool TryBegin(Entity<OrbitraRatvarVitalityComponent> sigil, EntityUid target)
    {
        if (sigil.Comp.Target != null || !CanAffect(sigil, target, out _)) return false;
        sigil.Comp.Target = target;
        sigil.Comp.Mind = _mind.TryGetMind(target, out var mind, out _) ? mind : null;
        sigil.Comp.FinishAt = _timing.CurTime + sigil.Comp.Interval;
        return true;
    }

    /// <summary>Rechecks cult, body, biological eligibility and continuous presence before every effect.</summary>
    public bool CanAffect(Entity<OrbitraRatvarVitalityComponent> sigil, EntityUid target,
        out Entity<OrbitraRatvarRuleComponent> cult)
    {
        cult = default;
        if (TerminatingOrDeleted(sigil) || EntityManager.IsQueuedForDeletion(sigil) || sigil.Comp.Interval <= TimeSpan.Zero ||
            !Transform(sigil).Anchored || _container.IsEntityInContainer(sigil) ||
            !TryComp<OrbitraRatvarStructureComponent>(sigil, out var structure) || structure.Rule is not { } owner ||
            !TryComp<OrbitraRatvarRuleComponent>(owner, out var rule) || !_ticker.IsGameRuleActive(owner) || rule.Won || rule.Lost ||
            TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target) ||
            !HasComp<BloodstreamComponent>(target) || !HasComp<DamageableComponent>(target) ||
            !TryComp<MobStateComponent>(target, out var mob) || _container.IsEntityInContainer(target) ||
            Transform(sigil).GridUid is not { } grid || Transform(target).GridUid != grid ||
            !_transform.InRange(sigil.Owner, target, sigil.Comp.Radius) ||
            !_interaction.InRangeUnobstructed(sigil.Owner, target, range: sigil.Comp.Radius)) return false;
        cult = (owner, rule);
        var hasMind = _mind.TryGetMind(target, out var mind, out _);
        if (hasMind && _job.MindTryGetJobId(mind, out var job) && job == "Chaplain") return false;
        if (_rule.TryGetCult(target, out var member) && member.Owner == owner)
            return mob.CurrentState != MobState.Dead || CanRevive(target);
        if (mob.CurrentState == MobState.Dead) return false;
        // До запуска ковчега сохраняем живых потенциальных новообращённых, независимо от помощника рядом.
        var convertible = hasMind && HasComp<HumanoidProfileComponent>(target) &&
            Comp<HumanoidProfileComponent>(target).Species == "Human" && !_role.MindIsAntagonist(mind) &&
            !(TryComp<MindShieldStatusComponent>(target, out var shield) && shield.IsMindshielded);
        return !convertible || rule.SummonAt != null;
    }

    private bool CanRevive(EntityUid target) =>
        TryComp<HumanoidProfileComponent>(target, out var profile) && profile.Species == "Human" &&
        !HasComp<OrbitraRatvarShellComponent>(target) && !HasComp<UnrevivableComponent>(target) && !_rotting.IsRotten(target) &&
        _mind.TryGetMind(target, out var mind, out var data) && data.OwnedEntity == target &&
        _role.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role) && !role.Value.Comp2.Marauder && !role.Value.Comp2.Builder;

    /// <summary>Performs one charged operation; body cooldown prevents duplicate effects from overlapping matrices.</summary>
    public bool TryPulse(Entity<OrbitraRatvarVitalityComponent> sigil)
    {
        if (sigil.Comp.Target is not { } target || sigil.Comp.FinishAt > _timing.CurTime ||
            !CanAffect(sigil, target, out var cult) ||
            (_mind.TryGetMind(target, out var mind, out _) ? mind : (EntityUid?) null) != sigil.Comp.Mind ||
            TryComp<OrbitraRatvarVitalityTargetComponent>(target, out var cooldown) && cooldown.NextPulse > _timing.CurTime)
            return false;
        EnsureComp<OrbitraRatvarVitalityTargetComponent>(target).NextPulse = _timing.CurTime + sigil.Comp.Interval;
        sigil.Comp.FinishAt = _timing.CurTime + sigil.Comp.Interval;
        if (_rule.TryGetCult(target, out var member) && member.Owner == cult.Owner)
            return TryHeal(sigil, target, cult);
        if (!_damageable.TryChangeDamage(target, sigil.Comp.Drain, out var dealt, origin: sigil) || dealt.GetTotal() <= 0)
            return false;
        var gain = sigil.Comp.Mind == null ? sigil.Comp.AnimalGain : sigil.Comp.MindGain;
        var fraction = Math.Clamp(dealt.GetTotal().Float() / Math.Max(0.01f, sigil.Comp.Drain.GetTotal().Float()), 0, 1);
        cult.Comp.Vitality = FixedPoint2.Min(cult.Comp.VitalityCapacity, cult.Comp.Vitality + gain * fraction);
        _stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(1));
        _adminLog.Add(LogType.Damaged, LogImpact.Medium, $"Ratvar vitality {ToPrettyString(sigil)} drained {ToPrettyString(target)} for {dealt.GetTotal()} damage.");
        return true;
    }

    private bool TryHeal(Entity<OrbitraRatvarVitalityComponent> sigil, EntityUid target, Entity<OrbitraRatvarRuleComponent> cult)
    {
        var patient = (target, Comp<DamageableComponent>(target));
        var injuries = _damageable.GetPositiveDamage(patient);
        if (Comp<MobStateComponent>(target).CurrentState == MobState.Dead)
        {
            var price = sigil.Comp.ReviveBase + injuries.GetTotal() * sigil.Comp.ReviveCost;
            if (cult.Comp.Vitality < price || !CanRevive(target)) return false;
            cult.Comp.Vitality -= price;
            _rejuvenate.PerformRejuvenate(target);
            if (_mind.TryGetMind(target, out var mind, out var data) && data.CurrentEntity != target &&
                _player.TryGetSessionById(data.UserId, out var session))
                _eui.OpenEui(new ReturnToBodyEui(data, _mind, _player), session);
            _adminLog.Add(LogType.Action, LogImpact.High, $"Ratvar vitality {ToPrettyString(sigil)} revived {ToPrettyString(target)}, spent {price} vitality.");
            return true;
        }
        var healing = new DamageSpecifier();
        var brute = _damageable.GetPositiveDamage(patient, Brute);
        var remaining = sigil.Comp.Healing;
        foreach (var (type, amount) in brute.DamageDict)
        {
            var part = FixedPoint2.Min(amount, remaining);
            healing.DamageDict[type] = -part;
            remaining -= part;
        }
        foreach (var type in HealingTypes)
            healing.DamageDict[type] = -FixedPoint2.Min(sigil.Comp.Healing, injuries.DamageDict.GetValueOrDefault(type));
        var reserve = -healing.GetTotal() * sigil.Comp.HealingCost;
        if (reserve <= 0 || cult.Comp.Vitality < reserve) return false;
        cult.Comp.Vitality -= reserve;
        if (!_damageable.TryChangeDamage(target, healing, out var healed, origin: sigil))
        {
            cult.Comp.Vitality += reserve;
            return false;
        }
        var cost = FixedPoint2.Clamp(-healed.GetTotal() * sigil.Comp.HealingCost, 0, reserve);
        cult.Comp.Vitality += reserve - cost;
        return cost > 0;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<OrbitraRatvarVitalityComponent>();
        while (query.MoveNext(out var uid, out var sigil))
        {
            if (sigil.Target is { } target)
            {
                if (!CanAffect((uid, sigil), target, out _) ||
                    (_mind.TryGetMind(target, out var mind, out _) ? mind : (EntityUid?) null) != sigil.Mind)
                {
                    sigil.Target = null;
                    sigil.Mind = null;
                }
                else if (_timing.CurTime >= sigil.FinishAt)
                {
                    TryPulse((uid, sigil));
                    sigil.FinishAt = _timing.CurTime + sigil.Interval;
                }
            }
            if (sigil.Target != null || _timing.CurTime < sigil.NextSearch) continue;
            sigil.NextSearch = _timing.CurTime + TimeSpan.FromSeconds(0.25);
            foreach (var candidate in _lookup.GetEntitiesInRange(Transform(uid).Coordinates, sigil.Radius))
                if (TryBegin((uid, sigil), candidate)) break;
        }
    }
}
