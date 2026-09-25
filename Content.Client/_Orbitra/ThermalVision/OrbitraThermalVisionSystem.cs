using Content.Shared._Orbitra.ThermalVision;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.ThermalVision;

/// <summary>Owns local thermal presentation and rejects stale contacts after equipment or body changes.</summary>
public sealed partial class OrbitraThermalVisionSystem : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    private OrbitraThermalWorldOverlay _world = default!;
    private OrbitraThermalContactOverlay _silhouettes = default!;
    private TimeSpan _received;
    internal TimeSpan ActivatedAt;
    internal OrbitraThermalContactsEvent? Contacts;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<OrbitraThermalContactsEvent>(OnContacts);
        _world = new(this);
        _silhouettes = new(this);
        _overlays.AddOverlay(_world);
        _overlays.AddOverlay(_silhouettes);
    }

    private void OnContacts(OrbitraThermalContactsEvent args)
    {
        if (_players.LocalEntity is not { } wearer || GetNetEntity(wearer) != args.Wearer) return;
        if (!args.Active)
        {
            if (Contacts?.Device == args.Device) Contacts = null;
            return;
        }
        if (Contacts?.Device != args.Device) ActivatedAt = _timing.RealTime;
        Contacts = args;
        _received = _timing.RealTime;
    }

    internal bool IsActive()
    {
        if (Contacts is not { } contacts) return false;
        if (_timing.RealTime - _received > TimeSpan.FromSeconds(0.35) ||
            _players.LocalEntity is not { } wearer || GetNetEntity(wearer) != contacts.Wearer ||
            !_inventory.TryGetSlotEntity(wearer, "eyes", out var eyes) || GetNetEntity(eyes.Value) != contacts.Device ||
            !TryComp<OrbitraThermalVisionComponent>(eyes, out var device) || !device.Enabled)
        {
            Contacts = null;
            return false;
        }
        return true;
    }

    /// <summary>Resolves only existing, live PVS sprites at their current interpolated transform.</summary>
    internal bool TryGetContactSprite(OrbitraThermalContact contact, out Entity<SpriteComponent> sprite)
    {
        sprite = default;
        if (!IsActive() || !TryGetEntity(contact.Target, out var target) || target is not { } uid ||
            !TryComp<SpriteComponent>(uid, out var component) || !component.Visible || component.ContainerOccluded ||
            component.Color.A <= 0 || (MetaData(uid).Flags & MetaDataFlags.Detached) != 0 ||
            _containers.IsEntityInContainer(uid) ||
            !TryComp<MobStateComponent>(uid, out var mob) || mob.CurrentState == MobState.Dead ||
            _players.LocalEntity is not { } wearer ||
            !_inventory.TryGetSlotEntity(wearer, "eyes", out var eyes) ||
            !TryComp<OrbitraThermalVisionComponent>(eyes, out var device)) return false;
        var origin = _transform.GetMapCoordinates(wearer);
        var position = _transform.GetMapCoordinates(uid);
        if (origin.MapId != position.MapId ||
            System.Numerics.Vector2.DistanceSquared(origin.Position, position.Position) > device.Range * device.Range) return false;
        sprite = (uid, component);
        return true;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        IsActive();
    }

    public override void Shutdown()
    {
        Contacts = null;
        _overlays.RemoveOverlay(_world);
        _overlays.RemoveOverlay(_silhouettes);
        _world.Dispose();
        _silhouettes.Dispose();
        base.Shutdown();
    }
}
