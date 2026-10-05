using Content.Shared._Orbitra.ThermalVision;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.PowerCell;
using Content.Shared.Temperature.Components;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.ThermalVision;

/// <summary>Authorizes same-map biological sprites for thermal rendering without expanding PVS.</summary>
public sealed partial class OrbitraThermalVisionSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, ICommonSession> _active = new();
    private readonly List<EntityUid> _stopping = new();
    private readonly List<EntityUid> _candidates = new();
    private readonly List<OrbitraThermalContact> _contacts = new();
    private TimeSpan _nextScan;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraThermalVisionComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<OrbitraThermalVisionComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<OrbitraThermalVisionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OrbitraThermalVisionComponent, PowerCellSlotEmptyEvent>(OnPowerCellEmpty);
        SubscribeLocalEvent<OrbitraToggleThermalVisionEvent>(OnToggle);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnEquipped(Entity<OrbitraThermalVisionComponent> ent, ref GotEquippedEvent args)
    {
        if (args.Slot != "eyes") return;
        ent.Comp.Wearer = args.EquipTarget;
        Dirty(ent);
        _actions.AddAction(args.EquipTarget, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
    }

    private void OnUnequipped(Entity<OrbitraThermalVisionComponent> ent, ref GotUnequippedEvent args)
    {
        if (args.Slot != "eyes") return;
        Disable(ent);
        _actions.RemoveAction(args.EquipTarget, ent.Comp.ActionEntity);
        ent.Comp.Wearer = null;
        Dirty(ent);
    }

    private void OnShutdown(Entity<OrbitraThermalVisionComponent> ent, ref ComponentShutdown args)
    {
        Disable(ent);
        if (ent.Comp.Wearer is { } wearer)
            _actions.RemoveAction(wearer, ent.Comp.ActionEntity);
    }

    private void OnToggle(OrbitraToggleThermalVisionEvent args)
    {
        if (args.Handled || args.Action.Comp.Container is not { } uid ||
            !TryComp<OrbitraThermalVisionComponent>(uid, out var device)) return;
        args.Handled = TryToggle((uid, device), args.Performer);
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        if (_inventory.TryGetSlotEntity(args.Entity, "eyes", out var eyes) &&
            TryComp<OrbitraThermalVisionComponent>(eyes, out var device))
            Disable((eyes.Value, device));
    }

    private void OnPowerCellEmpty(Entity<OrbitraThermalVisionComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        Disable(ent);
    }

    /// <summary>Toggles only an actual eye-slot device belonging to the performing player.</summary>
    public bool TryToggle(Entity<OrbitraThermalVisionComponent> device, EntityUid performer)
    {
        if (!CanUse(device, performer) || !TryComp<ActorComponent>(performer, out var actor) ||
            actor.PlayerSession.AttachedEntity != performer) return false;
        if (device.Comp.Enabled)
            Disable(device);
        else
        {
            if (!_powerCell.HasDrawCharge(device.Owner, user: performer))
                return false;

            device.Comp.Enabled = true;
            _powerCell.SetDrawEnabled(device.Owner, true);
            _active[device] = actor.PlayerSession;
            _actions.SetToggled(device.Comp.ActionEntity, true);
            Dirty(device);
            RefreshCandidates();
            SendContacts(device, performer, actor.PlayerSession);
        }
        return true;
    }

    private bool CanUse(Entity<OrbitraThermalVisionComponent> device, EntityUid wearer)
    {
        if (device.Comp.Wearer != wearer || !_inventory.TryGetSlotEntity(wearer, "eyes", out var eyes) || eyes != device.Owner)
            return false;
        var attempt = new OrbitraThermalVisionAttemptEvent(wearer);
        RaiseLocalEvent(device, ref attempt);
        return !attempt.Cancelled;
    }

    public void Disable(Entity<OrbitraThermalVisionComponent> device)
    {
        if (_active.Remove(device, out var session) && session.Status != SessionStatus.Disconnected &&
            device.Comp.Wearer is { } wearer)
            RaiseNetworkEvent(new OrbitraThermalContactsEvent(GetNetEntity(device), GetNetEntity(wearer), default, [], false), session);
        device.Comp.Enabled = false;
        _powerCell.SetDrawEnabled(device.Owner, false);
        _actions.SetToggled(device.Comp.ActionEntity, false);
        Dirty(device);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_active.Count == 0)
        {
            _candidates.Clear();
            return;
        }
        var scan = _timing.CurTime >= _nextScan;
        if (scan)
        {
            _nextScan = _timing.CurTime + TimeSpan.FromSeconds(0.05);
            RefreshCandidates();
        }
        _stopping.Clear();
        foreach (var (uid, session) in _active)
        {
            if (!TryComp<OrbitraThermalVisionComponent>(uid, out var device) || device.Wearer is not { } wearer ||
                session.AttachedEntity != wearer || !CanUse((uid, device), wearer))
            {
                _stopping.Add(uid);
                continue;
            }
            if (scan) SendContacts((uid, device), wearer, session);
        }
        foreach (var uid in _stopping)
        {
            if (TryComp<OrbitraThermalVisionComponent>(uid, out var device)) Disable((uid, device));
            else _active.Remove(uid);
        }
    }

    /// <summary>Checks physiology, containment and map without a device-specific distance limit.</summary>
    public bool IsThermalTarget(EntityUid target, EntityUid wearer)
    {
        if (target == wearer || TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target) ||
            !TryComp<MobStateComponent>(target, out var mob) || mob.CurrentState == MobState.Dead ||
            !HasComp<BloodstreamComponent>(target) || !HasComp<TemperatureComponent>(target) ||
            !TryComp<InjurableComponent>(target, out var damage) ||
            damage.DamageContainer?.Id is not ("Biological" or "StructuralBiological" or "BiologicalMetaphysical") ||
            _containers.IsEntityInContainer(target)) return false;
        // Тепловые контакты не раскрывают сущности из скрытых серверных слоёв видимости.
        var mask = MetaData(target).VisibilityMask;
        if (!TryComp<EyeComponent>(wearer, out var eye) || (eye.VisibilityMask & mask) != mask) return false;
        var origin = _transform.GetMapCoordinates(wearer);
        var position = _transform.GetMapCoordinates(target);
        return origin.MapId != MapId.Nullspace && origin.MapId == position.MapId;
    }

    private void RefreshCandidates()
    {
        // Один общий пакет кандидатов на интервал сканирования, не полный обход для каждых очков.
        _candidates.Clear();
        var query = EntityQueryEnumerator<BloodstreamComponent, TemperatureComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var mob))
        {
            if (mob.CurrentState != MobState.Dead)
                _candidates.Add(uid);
        }
    }

    private void SendContacts(Entity<OrbitraThermalVisionComponent> device, EntityUid wearer, ICommonSession session)
    {
        _contacts.Clear();
        var origin = _transform.GetMapCoordinates(wearer);
        foreach (var candidate in _candidates)
        {
            if (!IsThermalTarget(candidate, wearer)) continue;
            _contacts.Add(new OrbitraThermalContact(GetNetEntity(candidate)));
        }
        RaiseNetworkEvent(new OrbitraThermalContactsEvent(GetNetEntity(device), GetNetEntity(wearer), origin.MapId,
            _contacts.ToArray(), true), session);
    }

    public override void Shutdown()
    {
        _active.Clear();
        _candidates.Clear();
        _contacts.Clear();
        _stopping.Clear();
        base.Shutdown();
    }
}
