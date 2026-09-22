using System.Numerics;
using Content.Shared._Orbitra.Particles;
using Content.Shared.GameTicking;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Containers;
using Robust.Shared.Configuration;
using Robust.Client.Player;

namespace Content.Client._Orbitra.Particles;

/// <summary>Maintains one owned light impulse per weapon and bounded, deferred muzzle smoke.</summary>
public sealed partial class OrbitraGunEffectsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PointLightSystem _light = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private OrbitraParticleSystem _particles = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly Dictionary<EntityUid, ShotState> _shots = new();
    private readonly List<EntityUid> _expired = new();
    private EntityUid? _viewerMap;
    private bool _smokeEnabled = true;

    public override void Initialize()
    {
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => Clear());
        Subs.CVar(_configuration, OrbitraParticleCVars.Quality, quality =>
        {
            _smokeEnabled = quality != "Off";
            if (!_smokeEnabled)
                foreach (var shot in _shots.Values)
                    shot.Smoke.Clear();
        }, true);
    }

    /// <summary>Called once by the existing predicted/remote muzzle-flash path.</summary>
    public void ObserveShot(EntityUid gun, EntityUid source, Angle angle, string flash)
    {
        if (TerminatingOrDeleted(gun) || TerminatingOrDeleted(source))
            return;
        if (source == gun && _container.TryGetContainingContainer(gun, out var container))
            source = container.Owner;
        if (!_shots.TryGetValue(gun, out var shot))
        {
            if (_shots.Count >= 128)
                return;
            shot = new ShotState();
            _shots.Add(gun, shot);
            // Чужой PointLight не изменяем, даже если сейчас он выключен.
            if (!HasComp<PointLightComponent>(gun))
            {
                shot.Light = new PointLightComponent { NetSyncEnabled = false };
                AddComp(gun, shot.Light);
            }
        }
        TryComp<OrbitraGunEffectsComponent>(gun, out var profile);
        var now = _timing.RealTime;
        if (_smokeEnabled && _particles.Pool.Capacity > 0 && profile is { Smoke: true } && flash == "MuzzleFlashEffect")
            shot.Smoke.Record(now.TotalSeconds);
        else
            shot.Smoke.Clear();
        shot.Last = now;
        shot.Source = source;
        shot.Angle = angle;
        shot.Energy = Math.Clamp(profile?.Energy ?? 3f, 0, 8);
        if (shot.Light is { Deleted: false } light)
        {
            _light.SetColor(gun, flash == "MuzzleFlashEffect" ? profile?.Color ?? FlashColor(flash) : FlashColor(flash), light);
            _light.SetRadius(gun, Math.Clamp(profile?.Radius ?? 1.5f, 0.1f, 3f), light);
            _light.SetEnergy(gun, shot.Energy, light);
            _light.SetEnabled(gun, true, light);
        }
    }

    public static Color FlashColor(string flash) => flash switch
    {
        "MuzzleFlashEffectOmnilaser" => Color.FromHex("#AAE8FF"),
        "MuzzleFlashEffectHeavyLaser" => Color.FromHex("#FF7060"),
        _ => Color.FromHex("#FFE3A0"),
    };

    public override void FrameUpdate(float frameTime)
    {
        EntityUid? map = _player.LocalEntity is { } viewer && TryComp(viewer, out TransformComponent? viewerTransform)
            ? viewerTransform.MapUid : null;
        if (map != _viewerMap)
        {
            Clear();
            _viewerMap = map;
        }
        _expired.Clear();
        foreach (var (gun, shot) in _shots)
        {
            var elapsed = (_timing.RealTime - shot.Last).TotalSeconds;
            if (TerminatingOrDeleted(gun) || TerminatingOrDeleted(shot.Source) || elapsed > 1.5)
            {
                _expired.Add(gun);
                continue;
            }
            if (shot.Light is { Deleted: false } light && light.Enabled)
            {
                _light.SetEnergy(gun, shot.Energy * Math.Max(0, 1f - (float) elapsed / 0.1f), light);
                _light.SetEnabled(gun, elapsed < 0.1, light);
            }
            if (!shot.Smoke.Consume(_timing.RealTime.TotalSeconds))
                continue;
            if (elapsed > 0.8 || !TryComp(gun, out TransformComponent? xform) || xform.MapUid == null)
                continue;
            var source = _container.TryGetContainingContainer(gun, out var container) ? container.Owner : gun;
            var position = _transform.GetWorldPosition(xform) + shot.Angle.RotateVec(Vector2.UnitX * 0.5f);
            var point = _transform.ToCoordinates(xform.GridUid ?? xform.MapUid.Value, new MapCoordinates(position, xform.MapID));
            _particles.TryMuzzleSmoke(source, point);
        }
        foreach (var gun in _expired)
        {
            RemoveLight(gun, _shots[gun]);
            _shots.Remove(gun);
        }
    }

    private void RemoveLight(EntityUid gun, ShotState shot)
    {
        if (shot.Light is { Deleted: false } && !TerminatingOrDeleted(gun) &&
            TryComp<PointLightComponent>(gun, out var light) && ReferenceEquals(light, shot.Light))
            RemComp<PointLightComponent>(gun);
    }

    private void Clear()
    {
        foreach (var (gun, shot) in _shots)
            RemoveLight(gun, shot);
        _shots.Clear();
    }

    public override void Shutdown()
    {
        Clear();
        base.Shutdown();
    }

    private sealed class ShotState
    {
        public PointLightComponent? Light;
        public EntityUid Source;
        public Angle Angle;
        public TimeSpan Last;
        public readonly OrbitraMuzzleSmokeWindow Smoke = new();
        public float Energy;
    }
}
