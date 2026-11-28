using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Stacks;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Content.Shared.Wires;
using Robust.Shared.Containers;
using Robust.Shared.Localization;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Lavaland;

public sealed partial class OrbitraSalvageMechSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private PowerCellSystem _cells = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private VehicleSystem _vehicle = default!;

    private static readonly ProtoId<StackPrototype> LowVoltageCable = "Cable";

    [SubscribeLocalEvent]
    private void OnExamined(Entity<OrbitraSalvageMechComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(_loc.GetString(ent.Comp.DriveRepaired
            ? "orbitra-lavaland-ripley-drive-repaired"
            : "orbitra-lavaland-ripley-drive-broken"));
        if (!ent.Comp.DriveRepaired && TryComp<MechComponent>(ent, out var mech) && mech.EquipmentContainer.Count > 0)
            args.PushMarkup(_loc.GetString("orbitra-lavaland-ripley-strip"));
    }

    [SubscribeLocalEvent]
    private void OnCanRun(Entity<OrbitraSalvageMechComponent> ent, ref VehicleCanRunEvent args)
    {
        if (!ent.Comp.DriveRepaired || !HasPower(ent))
            args.CanRun = false;
    }

    [SubscribeLocalEvent]
    private void OnCockpitInsertion(Entity<OrbitraSalvageMechComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (!ent.Comp.DriveRepaired && TryComp<ContainerVehicleComponent>(ent, out var vehicle) &&
            args.Container.ID == vehicle.ContainerId)
            args.Cancel();
    }

    [SubscribeLocalEvent]
    private void OnStowAttempt(Entity<OrbitraSalvageMechComponent> ent, ref ContainerGettingInsertedAttemptEvent args)
    {
        if (!ent.Comp.DriveRepaired)
            args.Cancel();
    }

    [SubscribeLocalEvent]
    private void OnOperatorSet(Entity<OrbitraSalvageMechComponent> ent, ref VehicleOperatorSetEvent args)
    {
        _cells.SetDrawEnabled(ent.Owner, args.NewOperator != null);
    }

    [SubscribeLocalEvent]
    private void OnCellChanged(Entity<OrbitraSalvageMechComponent> ent, ref PowerCellChangedEvent args)
    {
        SyncPower(ent);
    }

    [SubscribeLocalEvent]
    private void OnChargeChanged(Entity<OrbitraSalvageMechComponent> ent, ref BatteryStateChangedEvent args)
    {
        SyncPower(ent);
    }

    // Штатная панель меха хранит отдельную копию заряда и не замечает внешний разряд батареи.
    private void SyncPower(Entity<OrbitraSalvageMechComponent> ent)
    {
        if (!TryComp<MechComponent>(ent, out var mech))
            return;

        var (charge, capacity) = _battery.GetCharge(ent.Owner);
        mech.Energy = charge;
        mech.MaxEnergy = capacity;
        Dirty(ent.Owner, mech);
        _vehicle.RefreshCanRun(ent.Owner);
    }

    private bool HasPower(EntityUid mech)
    {
        return _battery.GetCharge(mech).Charge > 0;
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<OrbitraSalvageMechComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.DriveRepaired || !IsRepairCable(args.Used))
            return;

        // Клик по неисправному приводу не должен проложить кабель на полу под мехом.
        args.Handled = true;
        if (!TryStartRepair(ent, args.User, args.Used) && !HasPower(ent))
            _popup.PopupEntity(_loc.GetString("orbitra-lavaland-ripley-needs-power"), ent.Owner, args.User);
    }

    public bool TryStartRepair(Entity<OrbitraSalvageMechComponent> ent, EntityUid user, EntityUid cable)
    {
        if (!CanRepair(ent, user, cable))
            return false;

        var repair = new DoAfterArgs(EntityManager, user, ent.Comp.RepairDuration, new OrbitraMechDriveRepairEvent(),
            ent.Owner, target: ent.Owner, used: cable)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        return _doAfter.TryStartDoAfter(repair);
    }

    private bool CanRepair(Entity<OrbitraSalvageMechComponent> ent, EntityUid user, EntityUid cable)
    {
        return !ent.Comp.DriveRepaired && IsRepairCable(cable) && _hands.IsHolding(user, cable) &&
               TryComp<WiresPanelComponent>(ent, out var panel) && panel.Open && HasPower(ent);
    }

    private bool IsRepairCable(EntityUid uid)
    {
        return TryComp<StackComponent>(uid, out var stack) && stack.StackTypeId == LowVoltageCable && stack.Count > 0;
    }

    [SubscribeLocalEvent]
    private void OnRepairComplete(Entity<OrbitraSalvageMechComponent> ent, ref OrbitraMechDriveRepairEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } cable || !CanRepair(ent, args.User, cable) ||
            !_stack.TryUse(cable, 1))
            return;

        ent.Comp.DriveRepaired = true;
        Dirty(ent);
        _physics.SetBodyType(ent.Owner, BodyType.KinematicController);
        _appearance.SetData(ent.Owner, OrbitraSalvageMechVisuals.DriveRepaired, true);
        _vehicle.RefreshCanRun(ent.Owner);
        _popup.PopupEntity(_loc.GetString("orbitra-lavaland-ripley-repair-success"), ent.Owner);
        args.Handled = true;
    }
}
