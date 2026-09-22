using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Orbitra.Particles;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Particles;

/// <summary>Samples ambient air without exciting atmos tiles or changing breathing cadence.</summary>
public sealed partial class OrbitraColdBreathSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private RespiratorSystem _respirator = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IGameTiming _timing = default!;
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<RespiratorComponent, ComponentShutdown>(OnRespiratorShutdown);
    }

    private void OnRespiratorShutdown(Entity<RespiratorComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<OrbitraColdBreathComponent>(ent, out var breath))
            SetIntensity((ent, breath), 0);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        var query = EntityQueryEnumerator<RespiratorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var respirator, out var transform))
        {
            var breath = EnsureComp<OrbitraColdBreathComponent>(uid);
            var intensity = GetIntensity((uid, respirator), transform);
            SetIntensity((uid, breath), intensity);
        }
    }

    public byte GetIntensity(Entity<RespiratorComponent> ent, TransformComponent transform)
    {
        if (!_mobState.IsAlive(ent) || !_respirator.IsBreathing((ent.Owner, ent.Comp)) ||
            transform.MapUid == null || _containers.IsEntityInContainer(ent))
            return 0;
        var air = _atmosphere.GetTileMixture((ent.Owner, transform), excite: false);
        return SharedOrbitraColdBreathSystem.Quantize(air?.Temperature, air?.Pressure ?? 0, true);
    }

    private void SetIntensity(Entity<OrbitraColdBreathComponent> ent, byte value)
    {
        if (ent.Comp.Intensity == value)
            return;
        ent.Comp.Intensity = value;
        Dirty(ent);
    }
}
