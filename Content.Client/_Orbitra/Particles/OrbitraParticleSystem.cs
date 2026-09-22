using System.Numerics;
using Content.Shared._Orbitra.Particles;
using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Tools.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.Particles;

/// <summary>Simulates cosmetic particles once per frame, independently of viewport rendering.</summary>
public sealed partial class OrbitraParticleSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private SharedOrbitraParticleSystem _geometry = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private IPlayerManager _players = default!;

    internal readonly OrbitraParticlePool Pool = new();
    private readonly Dictionary<(EntityUid, int), Emitter> _emitters = new();
    private readonly List<(EntityUid, int)> _stale = new();
    private readonly Random _random = new();
    private OrbitraParticleOverlay? _overlay;
    private float _density = 1f;
    private EntityUid? _viewerMap;
    private EntityQuery<AppearanceComponent> _appearanceQuery;
    private EntityQuery<DoAfterComponent> _doAfterQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;

    private struct Emitter
    {
        public TimeSpan LastSeen;
        public float Fraction;
    }

    public override void Initialize()
    {
        _appearanceQuery = GetEntityQuery<AppearanceComponent>();
        _doAfterQuery = GetEntityQuery<DoAfterComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        InitializeBreath();
        _overlay = new OrbitraParticleOverlay(EntityManager, this);
        _overlays.AddOverlay(_overlay);
        Subs.CVar(_configuration, OrbitraParticleCVars.Quality, PreviewQuality, true);
        SubscribeNetworkEvent<OrbitraParticleBurstEvent>(OnBurst);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => Clear());
    }

    public void PreviewQuality(string quality)
    {
        var (capacity, density) = OrbitraParticleCVars.GetBudget(quality);
        if (capacity != Pool.Capacity)
        {
            _overlay?.ClearCache();
            ClearAmbient();
        }
        Pool.Configure(capacity);
        _density = density;
        if (capacity == 0)
            Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        EntityUid? map = _players.LocalEntity is { } viewer && TryComp(viewer, out TransformComponent? viewerTransform)
            ? viewerTransform.MapUid : null;
        if (map != _viewerMap)
        {
            Clear();
            _viewerMap = map;
        }
        Pool.Update(frameTime);
        PruneImpactMarks();
        PruneAmbient();
        PruneBreath();
        _stale.Clear();
        foreach (var (key, emitter) in _emitters)
        {
            if (_timing.RealTime - emitter.LastSeen > TimeSpan.FromSeconds(0.1))
                _stale.Add(key);
        }
        foreach (var key in _stale)
            _emitters.Remove(key);
    }

    internal void Collect(EntityUid uid)
    {
        if (Pool.Capacity == 0)
            return;
        // TryGetData проверяет ключ, но отсутствие самого компонента логирует как ошибку.
        var burning = _appearanceQuery.TryComp(uid, out var appearance) &&
            _appearance.TryGetData<bool>(uid, FireVisuals.OnFire, out var onFire, appearance) && onFire;
        _doAfterQuery.TryComp(uid, out var actions);
        if (!burning && (actions == null || actions.DoAfters.Count == 0))
            return;
        if (!_transformQuery.TryComp(uid, out var xform) || xform.MapUid == null)
            return;
        if (burning)
        {
            _appearance.TryGetData<float>(uid, FireVisuals.FireStacks, out var stacks, appearance);
            var intensity = Math.Clamp(stacks / 3f, 0.5f, 1f);
            var point = _transform.ToCoordinates(xform.GridUid ?? xform.MapUid.Value, _transform.GetMapCoordinates(uid));
            Continuous((uid, -1), point, "OrbitraParticleEmber", Rate("OrbitraParticleEmber") * intensity, MathF.PI / 2);
            Continuous((uid, -2), point, "OrbitraParticleSmoke", Rate("OrbitraParticleSmoke") * intensity, MathF.PI / 2);
        }
        if (actions == null)
            return;
        foreach (var action in actions.DoAfters.Values)
        {
            if (!_tools.TryGetOrbitraParticleWork(action, out var effect, out var rate, out var point) ||
                _timing.CurTime >= action.StartTime + action.Args.Delay || action.Args.Hidden)
                continue;
            var direction = MathF.PI / 2;
            if (point == null)
            {
                if (action.Args.Target is not { } target || TerminatingOrDeleted(target) || Transform(target).MapUid == null)
                    continue;
                point = _geometry.Contact(target, uid, out direction);
                if (effect == "material")
                    effect = _geometry.MaterialEffect(target);
            }
            else if (effect == "material")
                effect = "OrbitraParticleDust";
            Continuous((uid, action.Index), point.Value, effect!, effect == "OrbitraParticleWelding" ? Rate(effect) : rate, direction);
            if (effect == "OrbitraParticleMetal" && rate >= 12)
                Continuous((uid, 131072 + action.Index), point.Value, "OrbitraParticleCuttingSpark", Rate("OrbitraParticleCuttingSpark"), direction);
            if (effect == "OrbitraParticleWelding")
                Continuous((uid, 65536 + action.Index), point.Value, "OrbitraParticleSmoke", Rate("OrbitraParticleSmoke"), MathF.PI / 2);
        }
    }

    private float Rate(string effect) => _prototypes.TryIndex<OrbitraParticleEffectPrototype>(effect, out var prototype)
        ? Math.Clamp(prototype.Rate, 0, 100) : 0;

    private void Continuous((EntityUid, int) key, EntityCoordinates point, string effect, float rate, float direction)
    {
        _emitters.TryGetValue(key, out var emitter);
        var dt = emitter.LastSeen == default ? 0f : (float) (_timing.RealTime - emitter.LastSeen).TotalSeconds;
        if (dt <= 0 && emitter.LastSeen != default)
            return;
        // Не догоняем время, проведённое за экраном или в зависшем кадре.
        var count = OrbitraParticlePool.AdvanceEmission(ref emitter.Fraction, dt, rate * _density);
        emitter.LastSeen = _timing.RealTime;
        _emitters[key] = emitter;
        Emit(point, effect, count, direction, false);
    }

    private void OnBurst(OrbitraParticleBurstEvent ev)
    {
        if (Pool.Capacity == 0 || !_prototypes.TryIndex<OrbitraParticleEffectPrototype>(ev.Effect, out var effect))
            return;
        if (TryGetEntity(ev.Source, out var source) && Exists(source.Value))
        {
            if (!TryComp<SpriteComponent>(source, out var sprite) || !sprite.Visible || sprite.ContainerOccluded || sprite.Color.A <= 0 ||
                (MetaData(source.Value).Flags & MetaDataFlags.Detached) != 0)
                return;
        }
        else if (ev.Effect is not ("OrbitraParticleElectrical" or "OrbitraParticleDestructionDust" or
                     "OrbitraParticleBulletMetal" or "OrbitraParticleBulletStone" or "OrbitraParticleBulletWood" or
                     "OrbitraParticleBulletGlass" or "OrbitraParticleBulletDust"))
            return;
        // При поломке конструкция заменяет исходную сущность. Координаты грида и FOV остаются действительными.
        var point = GetCoordinates(ev.Coordinates);
        if (!point.IsValid(EntityManager))
            return;
        Emit(point, ev.Effect, (int) MathF.Ceiling(effect.Count * _density), ev.Direction, true, ev.Tint);
        if (ev.ImpactMark && source is { } structure)
            TryImpactMark(structure, point);
    }

    private void Emit(EntityCoordinates point, string id, int count, float direction, bool burst, Color? tint = null)
    {
        if (count <= 0 || !_prototypes.TryIndex<OrbitraParticleEffectPrototype>(id, out var effect) ||
            !point.IsValid(EntityManager))
            return;
        count = Math.Min(count, 32);
        // Направление приходит в мировых координатах, позиции хранятся относительно грида/карты.
        var angle = direction - (float) _transform.GetWorldRotation(point.EntityId).Theta;
        for (var i = 0; i < count; i++)
        {
            var rotation = angle + ((float) _random.NextDouble() - 0.5f) * effect.Spread;
            var speed = effect.Speed * (0.4f + (float) _random.NextDouble() * 0.6f);
            Pool.Add(new OrbitraParticlePool.Particle
            {
                Parent = point.EntityId,
                Position = point.Position + new Vector2((float) _random.NextDouble() - 0.5f, (float) _random.NextDouble() - 0.5f) * effect.SpawnRadius * 2f,
                Origin = point.Position,
                Velocity = new Vector2(MathF.Cos(rotation), MathF.Sin(rotation)) * speed,
                Lifetime = effect.Lifetime * (0.6f + (float) _random.NextDouble() * 0.4f),
                Size = effect.Size * (0.8f + (float) _random.NextDouble() * 0.4f),
                Effect = effect,
                Burst = burst,
                Tint = tint,
            });
        }
    }

    private void Clear()
    {
        Pool.Clear();
        _emitters.Clear();
        _breaths.Clear();
        ClearAmbient();
    }

    public override void Shutdown()
    {
        Clear();
        if (_overlay != null)
        {
            _overlays.RemoveOverlay(_overlay);
            _overlay.Dispose();
            _overlay = null;
        }
        base.Shutdown();
    }
}
