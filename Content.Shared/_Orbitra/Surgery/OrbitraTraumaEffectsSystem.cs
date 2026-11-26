using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical.Healing;
using Content.Shared.Movement.Systems;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Surgery;

/// <summary>Предсказуемые последствия травм и выбор показания для полевой помощи.</summary>
public sealed partial class OrbitraTraumaEffectsSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private OrbitraSurgeryAnatomySystem _anatomy = default!;

    public static readonly SurgeryZone[] Zones = Enum.GetValues<SurgeryZone>();

    public override void Initialize()
    {
        SubscribeLocalEvent<OrbitraFittedProsthesesComponent, RefreshMovementSpeedModifiersEvent>(OnProstheticSpeed);
        SubscribeLocalEvent<OrbitraFittedProsthesesComponent, OrbitraHandActionDelayEvent>(OnProstheticHandAction);
        SubscribeLocalEvent<OrbitraTraumaComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<OrbitraTraumaComponent, OrbitraHandActionDelayEvent>(OnHandAction);
        SubscribeLocalEvent<OrbitraTraumaDressingComponent, AfterInteractEvent>(OnAfterInteract, before: [typeof(HealingSystem)]);
        SubscribeLocalEvent<OrbitraTraumaDressingComponent, UseInHandEvent>(OnUseInHand, before: [typeof(HealingSystem)]);
    }

    public SurgeryInjury GetInjuries(EntityUid patient, SurgeryZone zone)
    {
        if (!HasComp<OrbitraSurgicalPatientComponent>(patient))
            return SurgeryInjury.None;

        if (_anatomy.IsMissing(patient, zone))
            return SurgeryInjury.Missing;

        return TryComp<OrbitraTraumaComponent>(patient, out var trauma)
            ? trauma.Injuries.GetValueOrDefault(zone)
            : SurgeryInjury.None;
    }

    private void OnProstheticSpeed(Entity<OrbitraFittedProsthesesComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Parts.TryGetValue(SurgeryZone.LeftLeg, out var left) && !left ||
            ent.Comp.Parts.TryGetValue(SurgeryZone.RightLeg, out var right) && !right)
            args.ModifySpeed(0.9f);
    }

    private void OnProstheticHandAction(Entity<OrbitraFittedProsthesesComponent> ent, ref OrbitraHandActionDelayEvent args)
    {
        if (!_hands.TryGetHand(ent.Owner, _hands.GetActiveHand(ent.Owner), out var hand))
            return;

        var zone = hand.Value.Location switch
        {
            HandLocation.Left => SurgeryZone.LeftArm,
            HandLocation.Right => SurgeryZone.RightArm,
            _ => (SurgeryZone?) null,
        };
        if (zone is { } side && ent.Comp.Parts.TryGetValue(side, out var advanced) && !advanced)
            args.Multiplier *= 1.15f;
    }

    private void OnRefreshSpeed(Entity<OrbitraTraumaComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<OrbitraSurgicalPatientComponent>(ent, out var patient))
            return;

        var profile = _prototype.Index(patient.TraumaProfile);
        foreach (var (zone, injury) in ent.Comp.Injuries)
        {
            if (zone is SurgeryZone.LeftLeg or SurgeryZone.RightLeg && (injury & SurgeryInjury.Fracture) != 0)
                args.ModifySpeed((injury & SurgeryInjury.Splinted) != 0 ? profile.SplintedLegSpeed : profile.FracturedLegSpeed);
        }
    }

    private void OnHandAction(Entity<OrbitraTraumaComponent> ent, ref OrbitraHandActionDelayEvent args)
    {
        if (!_hands.TryGetHand(ent.Owner, _hands.GetActiveHand(ent.Owner), out var hand)
            || !TryComp<OrbitraSurgicalPatientComponent>(ent, out var patient))
            return;

        var zone = hand.Value.Location switch
        {
            HandLocation.Left => SurgeryZone.LeftArm,
            HandLocation.Right => SurgeryZone.RightArm,
            _ => (SurgeryZone?) null,
        };
        if (zone == null || !ent.Comp.Injuries.TryGetValue(zone.Value, out var injury) || (injury & SurgeryInjury.Fracture) == 0)
            return;

        var profile = _prototype.Index(patient.TraumaProfile);
        args.Multiplier *= (injury & SurgeryInjury.Splinted) != 0 ? profile.SplintedArmDelay : profile.FracturedArmDelay;
    }

    private void OnAfterInteract(Entity<OrbitraTraumaDressingComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Handled && args.CanReach && args.Target is {} patient)
            args.Handled = TryStartDressing(ent, args.User, patient);
    }

    private void OnUseInHand(Entity<OrbitraTraumaDressingComponent> ent, ref UseInHandEvent args)
    {
        if (!args.Handled)
            args.Handled = TryStartDressing(ent, args.User, args.User);
    }

    private bool TryStartDressing(Entity<OrbitraTraumaDressingComponent> item, EntityUid user, EntityUid patient)
    {
        if (!TryComp<OrbitraTraumaComponent>(patient, out var trauma))
            return false;

        foreach (var zone in Zones)
        {
            if (!CanDress(item, user, (patient, trauma), zone))
                continue;

            // Повторный щелчок может отменить наложение. Не передаём его обычному лечению бинтом.
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, item.Comp.Delay,
                new OrbitraTraumaDressingDoAfterEvent { Zone = zone }, item, target: patient, used: item)
            {
                NeedHand = true,
                BreakOnMove = true,
                DuplicateCondition = DuplicateConditions.SameTool,
            });
            return true;
        }

        return false;
    }

    public bool CanDress(Entity<OrbitraTraumaDressingComponent> item, EntityUid user,
        Entity<OrbitraTraumaComponent> patient, SurgeryZone zone)
    {
        if (!patient.Comp.Injuries.TryGetValue(zone, out var injury)
            || !HasComp<OrbitraSurgicalPatientComponent>(patient)
            || _anatomy.IsMissing(patient, zone)
            || (injury & item.Comp.Treatment) != 0)
            return false;

        var indicated = item.Comp.Treatment switch
        {
            SurgeryInjury.Bandaged => zone == SurgeryZone.Torso && (injury & SurgeryInjury.InternalBleeding) != 0,
            SurgeryInjury.Splinted => (injury & SurgeryInjury.Fracture) != 0,
            _ => false,
        };

        return indicated
            && !EntityManager.IsQueuedForDeletion(item)
            && (!TryComp<StackComponent>(item, out var stack) || stack.Unlimited || stack.Count > 0)
            && _hands.GetActiveItem(user) == item.Owner
            && _interaction.InRangeUnobstructed(user, patient.Owner);
    }
}
