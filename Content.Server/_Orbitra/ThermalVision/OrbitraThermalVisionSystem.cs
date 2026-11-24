using System.Runtime.InteropServices;
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

/// <summary>Разрешает тепловую отрисовку живых целей на карте владельца очков, не расширяя его PVS.</summary>
public sealed partial class OrbitraThermalVisionSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(0.05);
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(0.15);
    private static readonly TimeSpan FullRefreshInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<EntityUid, ActiveDevice> _active = new();
    private readonly List<EntityUid> _stopping = new();
    private readonly Dictionary<MapId, List<(ushort Mask, OrbitraThermalContact Contact)>> _candidates = new();
    private readonly Dictionary<(MapId Map, int Mask), Snapshot> _snapshots = new();
    private readonly List<OrbitraThermalContact> _contacts = new();
    private TimeSpan _nextScan;
    private int _scan;

    private readonly record struct Snapshot(int Scan, OrbitraThermalContact[] Contacts);

    private sealed class ActiveDevice(ICommonSession session)
    {
        public readonly ICommonSession Session = session;
        public MapId Map;
        public OrbitraThermalContact[]? Contacts;
        public TimeSpan NextKeepAlive;
        public TimeSpan NextFull;
    }

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

    /// <summary>Переключает очки, надетые самим игроком в слот глаз.</summary>
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
            var active = new ActiveDevice(actor.PlayerSession);
            _active[device] = active;
            _actions.SetToggled(device.Comp.ActionEntity, true);
            Dirty(device);
            RefreshIfDue(_timing.CurTime, force: _active.Count == 1);
            SendContacts(device, performer, active, _timing.CurTime);
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

    /// <summary>Выключает питание очков и отзывает разрешённые цели у их владельца.</summary>
    public void Disable(Entity<OrbitraThermalVisionComponent> device)
    {
        if (_active.Remove(device, out var active) && active.Session.Status != SessionStatus.Disconnected &&
            device.Comp.Wearer is { } wearer)
            RaiseNetworkEvent(new OrbitraThermalContactsEvent(GetNetEntity(device), GetNetEntity(wearer), default, [], false), active.Session);
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
            _snapshots.Clear();
            return;
        }
        var now = _timing.CurTime;
        var scan = RefreshIfDue(now);
        _stopping.Clear();
        foreach (var (uid, active) in _active)
        {
            if (!TryComp<OrbitraThermalVisionComponent>(uid, out var device) || device.Wearer is not { } wearer ||
                active.Session.AttachedEntity != wearer || !CanUse((uid, device), wearer))
            {
                _stopping.Add(uid);
                continue;
            }
            if (scan)
                SendContacts((uid, device), wearer, active, now);
        }
        foreach (var uid in _stopping)
        {
            if (TryComp<OrbitraThermalVisionComponent>(uid, out var device)) Disable((uid, device));
            else _active.Remove(uid);
        }
        if (scan)
        {
            foreach (var (key, snapshot) in _snapshots)
            {
                if (snapshot.Scan != _scan)
                    _snapshots.Remove(key);
            }
        }
    }

    /// <summary>Проверяет физиологию, контейнеры и слои видимости цели на карте владельца.</summary>
    public bool IsThermalTarget(EntityUid target, EntityUid wearer)
    {
        if (target == wearer || !TryComp<MobStateComponent>(target, out var mob) ||
            !HasComp<BloodstreamComponent>(target) || !HasComp<TemperatureComponent>(target) ||
            !CanDetect(target, mob) || !TryComp<EyeComponent>(wearer, out var eye))
            return false;

        var map = Transform(wearer).MapID;
        return map != MapId.Nullspace && map == Transform(target).MapID &&
            CanSeeLayer(eye.VisibilityMask, MetaData(target).VisibilityMask);
    }

    private bool CanDetect(EntityUid uid, MobStateComponent mob) =>
        mob.CurrentState != MobState.Dead && !TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid) &&
        TryComp<InjurableComponent>(uid, out var damage) &&
        (damage.DamageContainer?.Id is "Biological" or "StructuralBiological" or "BiologicalMetaphysical") &&
        !_containers.IsEntityInContainer(uid);

    // Тепловые контакты не раскрывают сущности из скрытых серверных слоёв видимости.
    private static bool CanSeeLayer(int eyeMask, ushort targetMask) => (eyeMask & targetMask) == targetMask;

    private bool RefreshIfDue(TimeSpan now, bool force = false)
    {
        if (!force && now < _nextScan)
            return false;

        _nextScan = now + ScanInterval;
        _scan++;
        foreach (var targets in _candidates.Values)
            targets.Clear();

        var query = EntityQueryEnumerator<BloodstreamComponent, TemperatureComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var mob, out var xform))
        {
            if (xform.MapID == MapId.Nullspace || !CanDetect(uid, mob))
                continue;

            if (!_candidates.TryGetValue(xform.MapID, out var targets))
                _candidates.Add(xform.MapID, targets = new());
            targets.Add((MetaData(uid).VisibilityMask, new OrbitraThermalContact(GetNetEntity(uid))));
        }

        foreach (var (map, targets) in _candidates)
        {
            if (targets.Count == 0)
                _candidates.Remove(map);
        }
        return true;
    }

    private OrbitraThermalContact[] GetContacts(MapId map, int mask)
    {
        var key = (map, mask);
        var hasSnapshot = _snapshots.TryGetValue(key, out var snapshot);
        if (hasSnapshot && snapshot.Scan == _scan)
            return snapshot.Contacts;

        _contacts.Clear();
        if (_candidates.TryGetValue(map, out var targets))
        {
            foreach (var target in targets)
            {
                if (CanSeeLayer(mask, target.Mask))
                    _contacts.Add(target.Contact);
            }
        }
        var contacts = hasSnapshot && CollectionsMarshal.AsSpan(_contacts).SequenceEqual(snapshot.Contacts)
            ? snapshot.Contacts
            : _contacts.ToArray();
        _snapshots[key] = new Snapshot(_scan, contacts);
        return contacts;
    }

    private void SendContacts(Entity<OrbitraThermalVisionComponent> device, EntityUid wearer, ActiveDevice active, TimeSpan now)
    {
        var map = Transform(wearer).MapID;
        var contacts = TryComp<EyeComponent>(wearer, out var eye) ? GetContacts(map, eye.VisibilityMask) : [];
        var full = active.Map != map || !ReferenceEquals(active.Contacts, contacts) || now >= active.NextFull;
        if (!full && now < active.NextKeepAlive)
            return;

        if (full)
        {
            active.Contacts = contacts;
            active.Map = map;
            active.NextFull = now + FullRefreshInterval;
        }
        // Короткое подтверждение продлевает список. Полный повтор восстанавливает его после пересоздания локального тела.
        active.NextKeepAlive = now + KeepAliveInterval;
        RaiseNetworkEvent(new OrbitraThermalContactsEvent(GetNetEntity(device), GetNetEntity(wearer), map,
            full ? contacts : [], true, keepAlive: !full), active.Session);
    }

    public override void Shutdown()
    {
        _active.Clear();
        _candidates.Clear();
        _snapshots.Clear();
        _contacts.Clear();
        _stopping.Clear();
        base.Shutdown();
    }
}
