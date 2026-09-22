using System.Numerics;
using Content.Shared._Orbitra.Particles;
using Content.Shared.Destructible;
using Content.Shared.Gravity;
using Content.Shared.GameTicking;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Throwing;
using Content.Shared.Wall;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Particles;

public sealed partial class OrbitraParticleBurstSystem
{
    // Пыль подтверждённых разрушений и тяжёлых воздействий; общая защита от повторов.
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;

    private static readonly ProtoId<ItemSizePrototype> HugeSize = "Huge";
    private readonly Dictionary<EntityUid, TimeSpan> _dustCooldowns = new();
    private readonly List<EntityUid> _expiredDust = new();
    private readonly HashSet<EntityUid> _damageDestructions = new();
    private TimeSpan _damageDestructionTime;

    private void InitializeDust()
    {
        SubscribeLocalEvent<ThrownItemComponent, LandEvent>(OnDustLand);
        SubscribeLocalEvent<PhysicsComponent, StartCollideEvent>(OnDustCollision);
        SubscribeLocalEvent<WallComponent, DestructionEventArgs>(OnDustDestruction);
        SubscribeLocalEvent<WallComponent, DamageThresholdReached>(OnWallDamageThreshold);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ClearDust());
    }

    private void OnDustLand(Entity<ThrownItemComponent> ent, ref LandEvent args)
    {
        if (ent.Comp.ThrownTime is { } start && _timing.CurTime - start >= TimeSpan.FromSeconds(0.2))
            TryImpactDust(ent);
    }

    private void OnDustCollision(Entity<PhysicsComponent> ent, ref StartCollideEvent args)
    {
        if (!args.OurFixture.Hard || !args.OtherFixture.Hard || args.PointCount == 0 ||
            HasComp<MobStateComponent>(args.OtherEntity) || HasComp<MobStateComponent>(ent) ||
            _container.IsEntityInContainer(args.OtherEntity))
            return;
        if (!CanImpactDust(ent) || IsDustCoolingDown(args.OtherEntity))
            return;
        var point = args.WorldPoints[0];
        var velocity = ContactVelocity(ent, point, args.OurBody);
        var otherVelocity = ContactVelocity(args.OtherEntity, point, args.OtherBody);
        // Robust передаёт одну нормаль A→B обоим участникам события.
        if (!args.OurFixture.Contacts.TryGetValue(args.OtherFixture, out var contact))
            return;
        var normal = contact.EntityA == ent.Owner ? args.WorldNormal : -args.WorldNormal;
        if (OrbitraDust.ClosingSpeed(velocity, otherVelocity, normal) < 2.5f)
            return;
        // Статическая стена не получает задержку: она не должна подавлять следующие предметы.
        if (TryImpactDust(ent) && CanImpactDust(args.OtherEntity))
            _dustCooldowns[args.OtherEntity] = _timing.CurTime + TimeSpan.FromSeconds(0.4);
    }

    private void OnDustDestruction(EntityUid uid, WallComponent component, DestructionEventArgs args)
    {
        if (_damageDestructionTime == _timing.CurTime && _damageDestructions.Remove(uid))
            TryDestructionDust(uid);
    }

    private void OnWallDamageThreshold(Entity<WallComponent> ent, ref DamageThresholdReached args)
    {
        if (_damageDestructionTime != _timing.CurTime)
        {
            _damageDestructions.Clear();
            _damageDestructionTime = _timing.CurTime;
        }
        foreach (var behavior in args.Threshold.Behaviors)
        {
            if (behavior is DoActsBehavior acts && acts.HasAct(ThresholdActs.Destruction))
                _damageDestructions.Add(ent);
        }
    }

    /// <summary>Emits floor-level dust for an eligible impact; shares cooldown with landing.</summary>
    public bool TryImpactDust(EntityUid uid)
    {
        if (!CanImpactDust(uid))
            return false;
        EmitDust(uid, "OrbitraParticleImpactDust");
        return true;
    }

    /// <summary>Publishes destruction dust before the source loses its transform.</summary>
    public bool TryDestructionDust(EntityUid uid)
    {
        if (!HasComp<WallComponent>(uid) || !CanDustAt(uid) || IsDustCoolingDown(uid))
            return false;
        EmitDust(uid, "OrbitraParticleDestructionDust");
        return true;
    }

    private bool CanImpactDust(EntityUid uid) =>
        !TerminatingOrDeleted(uid) && !IsDustCoolingDown(uid) && !HasComp<MobStateComponent>(uid) &&
        TryComp<PhysicsComponent>(uid, out var body) && !Transform(uid).Anchored &&
        (body.Mass >= 20f || TryComp<ItemComponent>(uid, out var item) &&
            _prototype.Index(item.Size) >= _prototype.Index(HugeSize)) && CanDustAt(uid);

    private bool CanDustAt(EntityUid uid)
    {
        if (!TryComp<TransformComponent>(uid, out var xform) || xform.GridUid is not { } grid ||
            _container.IsEntityInContainer(uid) || _gravity.IsWeightless(uid) ||
            !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;
        return !_map.GetTileRef(grid, gridComp, xform.Coordinates).Tile.IsEmpty;
    }

    private bool IsDustCoolingDown(EntityUid uid) =>
        _dustCooldowns.TryGetValue(uid, out var until) && until > _timing.CurTime;

    private void EmitDust(EntityUid uid, string effect)
    {
        var xform = Transform(uid);
        var point = _transform.ToCoordinates(xform.GridUid!.Value, _transform.GetMapCoordinates(uid, xform));
        var material = TryComp<OrbitraParticleMaterialComponent>(uid, out var mat) ? mat.Material : OrbitraParticleMaterial.Generic;
        _dustCooldowns[uid] = _timing.CurTime + TimeSpan.FromSeconds(0.4);
        Send(uid, point, effect, 0, OrbitraDust.MaterialColor(material));
    }

    private Vector2 ContactVelocity(EntityUid uid, Vector2 point, PhysicsComponent body)
    {
        var center = Vector2.Transform(body.LocalCenter, _transform.GetWorldMatrix(uid));
        var offset = point - center;
        return _physics.GetMapLinearVelocity(uid, body) +
            new Vector2(-offset.Y, offset.X) * _physics.GetMapAngularVelocity(uid, body);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_damageDestructionTime != _timing.CurTime)
            _damageDestructions.Clear();
        _expiredDust.Clear();
        foreach (var (uid, until) in _dustCooldowns)
        {
            if (until <= _timing.CurTime || Deleted(uid))
                _expiredDust.Add(uid);
        }
        foreach (var uid in _expiredDust)
            _dustCooldowns.Remove(uid);
    }

    private void ClearDust()
    {
        _dustCooldowns.Clear();
        _expiredDust.Clear();
        _damageDestructions.Clear();
    }

    public override void Shutdown()
    {
        ClearDust();
        base.Shutdown();
    }
}
