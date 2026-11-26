using Content.Shared._Orbitra.Surgery;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Orbitra.Surgery;

/// <summary>Создаёт локальные травмы после защиты и применяет их последствия до операции.</summary>
public sealed partial class OrbitraTraumaSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private OrbitraSurgeryAnatomySystem _anatomy = default!;
    [Dependency] private OrbitraTraumaEffectsSystem _effects = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;

    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Slash = "Slash";
    private static readonly ProtoId<DamageTypePrototype> Piercing = "Piercing";

    public event Action<EntityUid>? Changed;

    public override void Initialize()
    {
        SubscribeLocalEvent<OrbitraSurgicalPatientComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<OrbitraTraumaComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<OrbitraTraumaDressingComponent, OrbitraTraumaDressingDoAfterEvent>(OnDressingFinished);
    }

    public SurgeryInjury GetInjuries(EntityUid patient, SurgeryZone zone) => _effects.GetInjuries(patient, zone);

    private void OnDamageChanged(Entity<OrbitraSurgicalPatientComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.InterruptsDoAfters || args.DamageDelta == null)
            return;

        var damage = args.DamageDelta.DamageDict;
        var blunt = FixedPoint2.Max(0, damage.GetValueOrDefault(Blunt));
        var physical = blunt + FixedPoint2.Max(0, damage.GetValueOrDefault(Slash)) + FixedPoint2.Max(0, damage.GetValueOrDefault(Piercing));
        if (physical <= 0)
            return;

        var profile = _prototype.Index(ent.Comp.TraumaProfile);
        if (Roll(physical, profile.Dismemberment)
            && TryPickZone(ent, SurgeryInjury.Missing, limbsOnly: true, out var lost))
        {
            var attempt = new OrbitraDismemberAttemptEvent(lost);
            RaiseLocalEvent(ent, ref attempt);
            if (attempt.Handled)
                return;
        }

        if (Roll(blunt, profile.Fracture) && TryPickZone(ent, SurgeryInjury.Fracture, limbsOnly: true, out var limb))
            AddInjury(ent, limb, SurgeryInjury.Fracture);

        if (Roll(physical, profile.InternalBleeding)
            && _anatomy.SupportsZone(ent, SurgeryZone.Torso) && !_anatomy.IsMissing(ent, SurgeryZone.Torso))
            AddInjury(ent, SurgeryZone.Torso, SurgeryInjury.InternalBleeding);
    }

    /// <summary>Вызывается после попадания снаряда, когда уже известен урон с учётом брони.</summary>
    public void OnProjectileDamage(EntityUid patient, EntityUid projectile, DamageSpecifier damage)
    {
        if (!TryComp<OrbitraSurgicalPatientComponent>(patient, out var surgical)
            || !damage.DamageDict.TryGetValue(Piercing, out var piercing)
            || MetaData(projectile).EntityPrototype is not {} source)
            return;

        var profile = _prototype.Index(surgical.TraumaProfile);
        if (profile.FragmentProjectiles.Contains(source.ID) && Roll(piercing, profile.Shrapnel)
            && TryPickZone(patient, SurgeryInjury.Shrapnel, limbsOnly: false, out var zone))
            AddInjury(patient, zone, SurgeryInjury.Shrapnel);
    }

    private bool Roll(FixedPoint2 damage, OrbitraTraumaRoll roll)
        => damage > 0 && damage >= roll.Threshold && _random.Prob(roll.Chance);

    private bool TryPickZone(EntityUid patient, SurgeryInjury injury, bool limbsOnly, out SurgeryZone zone)
    {
        Span<SurgeryZone> eligible = stackalloc SurgeryZone[OrbitraTraumaEffectsSystem.Zones.Length];
        var count = 0;
        foreach (var candidate in OrbitraTraumaEffectsSystem.Zones)
        {
            if (limbsOnly && candidate is SurgeryZone.Head or SurgeryZone.Torso)
                continue;

            if (!_anatomy.SupportsZone(patient, candidate)
                || (GetInjuries(patient, candidate) & (SurgeryInjury.Missing | injury)) != 0)
                continue;

            eligible[count++] = candidate;
        }
        zone = count == 0 ? default : eligible[_random.Next(count)];
        return count != 0;
    }

    private void AddInjury(EntityUid patient, SurgeryZone zone, SurgeryInjury injury)
    {
        var trauma = EnsureComp<OrbitraTraumaComponent>(patient);
        var existing = trauma.Injuries.GetValueOrDefault(zone);
        if ((existing & injury) != 0)
            return;

        trauma.Injuries[zone] = existing | injury;
        NotifyChanged((patient, trauma));
    }

    public bool TryTreat(EntityUid patient, SurgeryZone zone, SurgeryInjury injury)
    {
        if (injury is not (SurgeryInjury.Fracture or SurgeryInjury.InternalBleeding or SurgeryInjury.Shrapnel)
            || !HasComp<OrbitraSurgicalPatientComponent>(patient)
            || !TryComp<OrbitraTraumaComponent>(patient, out var trauma)
            || !trauma.Injuries.TryGetValue(zone, out var existing) || (existing & injury) == 0)
            return false;

        var dressing = injury switch
        {
            SurgeryInjury.Fracture => SurgeryInjury.Splinted,
            SurgeryInjury.InternalBleeding => SurgeryInjury.Bandaged,
            _ => SurgeryInjury.None,
        };
        if (dressing != SurgeryInjury.None)
            trauma.StabilizationRemaining.Remove(zone);

        var remaining = existing & ~(injury | dressing);
        if (remaining == SurgeryInjury.None)
            trauma.Injuries.Remove(zone);
        else
            trauma.Injuries[zone] = remaining;
        NotifyChanged((patient, trauma));
        return true;
    }

    public void NotifyAnatomyChanged(EntityUid patient, SurgeryZone zone, bool missing)
    {
        if (missing && TryComp<OrbitraTraumaComponent>(patient, out var trauma) && trauma.Injuries.Remove(zone))
        {
            trauma.StabilizationRemaining.Remove(zone);
            NotifyChanged((patient, trauma));
            return;
        }
        Changed?.Invoke(patient);
    }

    private void NotifyChanged(Entity<OrbitraTraumaComponent> ent)
    {
        if (ent.Comp.Injuries.Count == 0)
            RemComp<OrbitraTraumaComponent>(ent);
        else
            Dirty(ent);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
        Changed?.Invoke(ent.Owner);
    }

    private void OnRejuvenate(Entity<OrbitraTraumaComponent> ent, ref RejuvenateEvent args)
    {
        ent.Comp.Injuries.Clear();
        NotifyChanged(ent);
    }

    private void OnDressingFinished(Entity<OrbitraTraumaDressingComponent> item, ref OrbitraTraumaDressingDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used != item.Owner || args.Target is not {} target
            || !TryComp<OrbitraTraumaComponent>(target, out var trauma)
            || !TryComp<OrbitraSurgicalPatientComponent>(target, out var patient)
            || !_effects.CanDress(item, args.User, (target, trauma), args.Zone))
            return;

        if (TryComp<StackComponent>(item, out var stack))
        {
            if (!_stack.TryUse((item, stack), 1))
                return;
        }
        else
            QueueDel(item);

        var profile = _prototype.Index(patient.TraumaProfile);
        var splint = item.Comp.Treatment == SurgeryInjury.Splinted;
        trauma.StabilizationRemaining[args.Zone] = splint ? profile.SplintSeconds : profile.DressingSeconds;
        trauma.Injuries[args.Zone] |= item.Comp.Treatment;
        NotifyChanged((target, trauma));
        _popup.PopupEntity(Loc.GetString(splint ? "orbitra-trauma-splinted" : "orbitra-trauma-bandaged",
            ("zone", Loc.GetString($"orbitra-surgery-zone-{args.Zone.ToString().ToLowerInvariant()}"))), target, args.User);
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<OrbitraTraumaComponent, OrbitraSurgicalPatientComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var trauma, out var patient, out var blood))
        {
            var profile = _prototype.Index(patient.TraumaProfile);
            // Штатная скорость кровообращения учитывает стазис.
            trauma.Elapsed += frameTime / Math.Max(0.01f, blood.UpdateIntervalMultiplier);
            if (trauma.Elapsed < profile.TickSeconds)
                continue;

            var elapsed = trauma.Elapsed;
            trauma.Elapsed = 0f;
            var expired = false;
            var fragments = 0;
            foreach (var zone in OrbitraTraumaEffectsSystem.Zones)
            {
                if (!trauma.Injuries.TryGetValue(zone, out var injury))
                    continue;

                if (trauma.StabilizationRemaining.TryGetValue(zone, out var remaining))
                {
                    if (remaining > elapsed)
                        trauma.StabilizationRemaining[zone] = remaining - elapsed;
                    else
                    {
                        trauma.StabilizationRemaining.Remove(zone);
                        trauma.Injuries[zone] = injury &= ~(SurgeryInjury.Bandaged | SurgeryInjury.Splinted);
                        expired = true;
                    }
                }

                if ((injury & SurgeryInjury.InternalBleeding) != 0)
                    _bloodstream.TryModifyBloodLevel((uid, blood),
                        -((injury & SurgeryInjury.Bandaged) != 0 ? profile.BandagedBloodLoss : profile.BloodLoss));
                if ((injury & SurgeryInjury.Shrapnel) != 0)
                    fragments++;
            }

            if (expired)
                NotifyChanged((uid, trauma));
            if (fragments > 0 && !_mobState.IsDead(uid))
                _damageable.TryChangeDamage(uid, profile.FragmentDamage * fragments, ignoreResistances: true, interruptsDoAfters: false);
        }
    }
}
