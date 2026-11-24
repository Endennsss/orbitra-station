using Content.Server._Orbitra.Particles;
using Content.Shared.Atmos;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] private OrbitraParticleBurstSystem _orbitraFireParticles = default!;

    private TimeSpan _nextFireParticleWindow;
    private int _fireParticleBursts;

    private void PlayOrbitraFireEnd(EntityUid grid, TileAtmosphere tile, bool quenched)
    {
        if (!tile.Hotspot.Valid || tile.Hotspot.State == 0 || (!quenched && tile.Hotspot.State < 2))
            return;

        var now = _gameTiming.CurTime;
        if (now >= _nextFireParticleWindow)
        {
            _nextFireParticleWindow = now + TimeSpan.FromSeconds(0.2);
            _fireParticleBursts = 0;
        }

        if (_fireParticleBursts >= 12 || !HasComp<MapGridComponent>(grid))
            return;

        _fireParticleBursts++;
        var point = _mapSystem.ToCenterCoordinates(grid, tile.GridIndices);
        _orbitraFireParticles.EmitEnvironment(point, quenched ? "OrbitraParticleQuenchSteam" : "OrbitraParticleCoolingEmber", MathF.PI / 2);
    }
}
