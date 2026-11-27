using System.Numerics;
using Content.Shared._Orbitra.Particles;
using Content.Shared.CCVar;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Utility;

namespace Content.Client._Orbitra.Particles;

public sealed partial class OrbitraParticleSystem
{
    [Dependency] private ILightManager _lights = default!;
    internal static readonly TimeSpan TrailFade = TimeSpan.FromMilliseconds(80);
    internal readonly Dictionary<EntityUid, BulletTrail> Flying = new();
    internal readonly List<BulletTrail> Fading = new();
    private readonly Stack<BulletTrail> _spareTrails = new();
    private OrbitraBulletTrailOverlay _bulletOverlay = default!;
    private int _trailLimit;
    private bool _lowTrails;
    private bool _reducedTrails;

    internal sealed class BulletTrail
    {
        public readonly OrbitraBulletTrailPath Path = new();
        public EntityUid Parent;
        public Color Tint;
        public bool Thin;
        public TimeSpan LastMotion;
        public TimeSpan Ended;
    }

    private void InitializeBulletTrails()
    {
        _bulletOverlay = new OrbitraBulletTrailOverlay(this, EntityManager, _prototypes, _timing, _lights);
        _overlays.AddOverlay(_bulletOverlay);
        SubscribeLocalEvent<OrbitraBulletTrailComponent, EntityTerminatingEvent>(OnBulletTerminating);
        SubscribeLocalEvent<OrbitraBulletTrailComponent, ComponentShutdown>(OnTrailShutdown);
        SubscribeLocalEvent<OrbitraBulletTrailComponent, EntParentChangedMessage>(OnTrailParentChanged);
        Subs.CVar(_configuration, CCVars.ReducedMotion, reduced =>
        {
            _reducedTrails = reduced;
            ClearBulletTrails();
        }, true);
    }

    private void ConfigureBulletTrails(string quality)
    {
        var limit = quality switch { "Off" => 0, "Low" => 32, "High" => 160, _ => 96 };
        if (_trailLimit == limit)
            return;
        _trailLimit = limit;
        _lowTrails = quality == "Low";
        ClearBulletTrails();
    }

    private void ClearBulletTrails()
    {
        foreach (var trail in Flying.Values)
            _spareTrails.Push(trail);
        Flying.Clear();
        foreach (var trail in Fading)
            _spareTrails.Push(trail);
        Fading.Clear();
    }

    private void ForgetTrail(EntityUid uid)
    {
        if (Flying.Remove(uid, out var trail))
            _spareTrails.Push(trail);
    }

    private void OnTrailParentChanged(Entity<OrbitraBulletTrailComponent> ent, ref EntParentChangedMessage args)
    {
        // При удалении движок также отсоединяет пулю, но флаг Detached ставит только при выходе из PVS.
        if (args.Transform.ParentUid.IsValid() || (MetaData(ent).Flags & MetaDataFlags.Detached) != 0)
            ForgetTrail(ent);
    }

    private void OnTrailShutdown(Entity<OrbitraBulletTrailComponent> ent, ref ComponentShutdown args)
        => ForgetTrail(ent);

    private void OnBulletTerminating(Entity<OrbitraBulletTrailComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!Flying.Remove(ent, out var trail))
            return;

        var now = _timing.RealTime;
        if (_reducedTrails || args.Entity.Comp.EntityPaused ||
            (args.Entity.Comp.Flags & (MetaDataFlags.Detached | MetaDataFlags.InContainer)) != 0 ||
            trail.Path.Count < 2 || now - trail.LastMotion >= TrailFade)
        {
            _spareTrails.Push(trail);
            return;
        }

        trail.Ended = now;
        Fading.Add(trail);
    }

    private void UpdateBulletTrails(EntityUid? map)
    {
        if (_trailLimit == 0 || map == null)
            return;

        var now = _timing.RealTime;
        for (var i = Fading.Count - 1; i >= 0; i--)
        {
            if (now - Fading[i].Ended < TrailFade)
                continue;
            _spareTrails.Push(Fading[i]);
            Fading.RemoveSwap(i);
        }

        var query = AllEntityQuery<OrbitraBulletTrailComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var profile, out var xform, out var meta))
        {
            var pixels = OrbitraBulletTrailPath.VisiblePixels(profile.LengthPixels, _lowTrails, _reducedTrails);
            if (pixels == 0 || meta.EntityPaused || xform.MapUid != map || !xform.ParentUid.IsValid() ||
                (meta.Flags & (MetaDataFlags.Detached | MetaDataFlags.InContainer)) != 0 ||
                !_spriteQuery.TryComp(uid, out var sprite) || !sprite.Visible || sprite.ContainerOccluded || sprite.Color.A <= 0)
            {
                ForgetTrail(uid);
                continue;
            }

            var position = xform.LocalPosition;
            if (!Flying.TryGetValue(uid, out var trail))
            {
                if (Flying.Count + Fading.Count >= _trailLimit)
                    continue;
                trail = _spareTrails.TryPop(out var spare) ? spare : new BulletTrail();
                Flying.Add(uid, trail);
                trail.Parent = xform.ParentUid;
                trail.Path.Reset(position);
                trail.LastMotion = now;
            }
            var previous = trail.Path.GetPoint(trail.Path.Count - 1);
            // Разрыв координат или смена грида не должны рисовать линию через стену или между картами.
            if (trail.Parent != xform.ParentUid || Vector2.DistanceSquared(position, previous) > 16f)
            {
                trail.Parent = xform.ParentUid;
                trail.Path.Reset(position);
            }
            else if (position != previous)
            {
                trail.Path.Observe(position, pixels / (float) EyeManager.PixelsPerMeter);
                trail.LastMotion = now;
            }
            else if (now - trail.LastMotion >= TrailFade)
                trail.Path.Reset(position);

            trail.Tint = profile.Tint.WithAlpha(profile.Tint.A * sprite.Color.A);
            trail.Thin = _lowTrails || _reducedTrails || pixels <= 6;
        }
    }
}
