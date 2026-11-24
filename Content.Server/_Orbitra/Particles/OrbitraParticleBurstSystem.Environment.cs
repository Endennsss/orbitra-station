using Content.Server.Medical.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Medical.Cryogenics;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server._Orbitra.Particles;

public sealed partial class OrbitraParticleBurstSystem
{
    [Dependency] private SharedGasTileOverlaySystem _gasOverlay = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextVentParticle = new();
    private TimeSpan _nextVentCleanup;

    /// <summary>Посылает ближайшим клиентам выброс частиц. Направление задано в мировых радианах.</summary>
    public void EmitEnvironment(EntityCoordinates point, string effect, float direction, EntityUid? source = null)
    {
        if (point.IsValid(EntityManager))
            Send(source ?? EntityUid.Invalid, point, effect, direction);
    }

    /// <summary>Суммирует количество газов из списка видимых примесей. Значение выражено в молях.</summary>
    public float VisibleGasMoles(GasMixture? mixture)
    {
        if (mixture == null)
            return 0f;

        var moles = 0f;
        foreach (var gas in _gasOverlay.VisibleGasId)
            moles += mixture[gas];
        return moles;
    }

    /// <summary>Показывает приток или вытяжку с ограничением частоты на вентиляцию и общего числа источников.</summary>
    public void EmitVentFlow(EntityUid uid, bool intake)
    {
        var now = _timing.CurTime;
        if (_nextVentParticle.TryGetValue(uid, out var next) && now < next)
            return;

        if (_nextVentParticle.Count >= 256 && now < _nextVentCleanup)
            return;

        if (now >= _nextVentCleanup)
        {
            _nextVentCleanup = now + TimeSpan.FromSeconds(3);
            _nextVentParticle.Clear();
        }

        var xform = Transform(uid);
        if (xform.GridUid is not { } grid)
            return;

        _nextVentParticle[uid] = now + TimeSpan.FromSeconds(0.8);
        var point = _transform.ToCoordinates(grid, _transform.GetMapCoordinates(xform));
        EmitEnvironment(point, intake ? "OrbitraParticleVentIntake" : "OrbitraParticleVentSupply", 0f, uid);
    }

    private void OnCryoOpened(Entity<CryoPodAirComponent> pod, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != CryoPodComponent.BodyContainerName || TerminatingOrDeleted(pod) ||
            pod.Comp.Air.Temperature >= Atmospherics.T0C || pod.Comp.Air.TotalMoles <= 0.1f)
            return;

        var xform = Transform(pod);
        if (xform.GridUid is not { } grid)
            return;

        var point = _transform.ToCoordinates(grid, _transform.GetMapCoordinates(xform));
        EmitEnvironment(point, "OrbitraParticleCryoMist", (float) _transform.GetWorldRotation(xform).Theta, pod);
    }
}
