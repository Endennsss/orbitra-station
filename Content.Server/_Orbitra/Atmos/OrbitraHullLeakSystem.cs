using Content.Server.Atmos.EntitySystems;
using Content.Shared._Orbitra.Atmos;
using Content.Shared._Orbitra.Particles;
using Content.Shared.Atmos;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Atmos;

public sealed partial class OrbitraHullLeakSystem : EntitySystem
{
    private static readonly ProtoId<ToolQualityPrototype> Welding = "Welding";
    private static readonly SoundPathSpecifier HissSound = new("/Audio/Ambience/Objects/gas_hiss.ogg");
    private static readonly TimeSpan LeakInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan PatchDuration = TimeSpan.FromMinutes(2);

    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraHullLeakComponent, MapInitEvent>(OnMapInit);
#pragma warning disable CS0618 // Событие даёт итоговый урон и вызывается при ремонте.
        SubscribeLocalEvent<OrbitraHullLeakComponent, DamageChangedEvent>(OnDamage);
#pragma warning restore CS0618
        SubscribeLocalEvent<OrbitraHullLeakComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<OrbitraHullLeakComponent, OrbitraHullPatchDoAfterEvent>(OnPatchFinished);
        SubscribeLocalEvent<OrbitraHullLeakComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<OrbitraActiveHullLeakComponent, ComponentShutdown>(OnLeakShutdown);
    }

#pragma warning disable CS0618 // Для обшивки используем тот же суммарный урон, что и порог разрушения.
    private void OnMapInit(Entity<OrbitraHullLeakComponent> wall, ref MapInitEvent args)
    {
        if (_damageable.GetTotalDamage(wall.Owner) >= wall.Comp.DamageThreshold)
            EnsureComp<OrbitraActiveHullLeakComponent>(wall);
    }

    private void OnDamage(Entity<OrbitraHullLeakComponent> wall, ref DamageChangedEvent args)
    {
        if (TerminatingOrDeleted(wall))
            return;
        if (_damageable.GetTotalDamage((wall.Owner, args.Damageable)) >= wall.Comp.DamageThreshold)
            EnsureComp<OrbitraActiveHullLeakComponent>(wall);
        else
            RemCompDeferred<OrbitraActiveHullLeakComponent>(wall);
    }
#pragma warning restore CS0618

    private void OnInteractUsing(Entity<OrbitraHullLeakComponent> wall, ref InteractUsingEvent args)
    {
        if (!args.Handled && TryPatch(wall, args.User, args.Used))
            args.Handled = true;
    }

    public bool TryPatch(Entity<OrbitraHullLeakComponent> wall, EntityUid user, EntityUid tool) =>
        CanPatch(wall) && _tool.UseTool(tool, user, wall, 3f, Welding, new OrbitraHullPatchDoAfterEvent(), fuel: 2f);

    private bool CanPatch(Entity<OrbitraHullLeakComponent> wall) =>
        _timing.CurTime >= wall.Comp.PatchedUntil && TryComp<OrbitraActiveHullLeakComponent>(wall, out var active) && active.Flowing;

    private void OnPatchFinished(Entity<OrbitraHullLeakComponent> wall, ref OrbitraHullPatchDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !CanPatch(wall))
            return;

        args.Handled = true;
        wall.Comp.PatchedUntil = _timing.CurTime + PatchDuration;
        if (TryComp<OrbitraActiveHullLeakComponent>(wall, out var active))
            StopHiss(active);
        _popup.PopupEntity(_loc.GetString("orbitra-hull-patch-finished"), wall, args.User);
    }

    private void OnExamine(Entity<OrbitraHullLeakComponent> wall, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (_timing.CurTime < wall.Comp.PatchedUntil)
            args.PushMarkup(_loc.GetString("orbitra-hull-patch-examine"));
        else if (CanPatch(wall))
            args.PushMarkup(_loc.GetString("orbitra-hull-leak-examine"));
    }

    private void OnLeakShutdown(Entity<OrbitraActiveHullLeakComponent> leak, ref ComponentShutdown args) =>
        StopHiss(leak.Comp);

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var leaks = EntityQueryEnumerator<OrbitraActiveHullLeakComponent, OrbitraHullLeakComponent, TransformComponent>();
        while (leaks.MoveNext(out var uid, out var active, out var wall, out var xform))
        {
            if (now < active.NextUpdate)
                continue;
            active.NextUpdate = now + LeakInterval;
            if (now < wall.PatchedUntil || !xform.Anchored || xform.GridUid is not { } grid ||
                !_transform.TryGetGridTilePosition((uid, xform), out var wallTile) ||
                !TryGetInterior(grid, xform.MapUid, wallTile, out var interior) ||
                _atmosphere.GetTileMixture(grid, xform.MapUid, interior, excite: true) is not { Immutable: false } air)
            {
                StopHiss(active);
                continue;
            }

            var moles = wall.MolesPerSecond * (float) LeakInterval.TotalSeconds *
                        MathF.Sqrt(Math.Clamp(air.Pressure / Atmospherics.OneAtmosphere, 0f, 1f));
            air.Multiply(1f - Math.Clamp(moles / air.TotalMoles, 0f, 1f));
            active.Flowing = true;
            active.Hiss ??= _audio.PlayPvs(HissSound, uid,
                AudioParams.Default.WithLoop(true).WithVolume(-18f).WithMaxDistance(5f))?.Entity;
            if (now >= active.NextMist)
            {
                active.NextMist = now + TimeSpan.FromSeconds(2);
                var offset = wallTile - interior;
                var angle = MathF.Atan2(offset.Y, offset.X) + (float) _transform.GetWorldRotation(grid).Theta;
                var point = _map.ToCenterCoordinates(grid, interior);
                RaiseNetworkEvent(new OrbitraParticleBurstEvent(NetEntity.Invalid, GetNetCoordinates(point),
                    "OrbitraParticleLeakMist", angle), Filter.Pvs(point, 0.5f, EntityManager));
            }
        }
    }

    private bool TryGetInterior(EntityUid grid, EntityUid? map, Vector2i wall, out Vector2i interior)
    {
        for (var i = 0; i < Atmospherics.Directions; i++)
        {
            var offset = ((AtmosDirection) (1 << i)).CardinalToIntVec();
            interior = wall - offset;
            if (_atmosphere.IsTileSpace(grid, map, wall + offset) &&
                !_atmosphere.IsTileSpace(grid, map, interior) &&
                _atmosphere.GetTileMixture(grid, map, interior) is { Immutable: false, Pressure: > 1f })
                return true;
        }

        interior = default;
        return false;
    }

    private void StopHiss(OrbitraActiveHullLeakComponent active)
    {
        active.Flowing = false;
        active.Hiss = _audio.Stop(active.Hiss);
    }
}
