using Content.Server._Orbitra.Particles;
using Content.Shared.Atmos;

namespace Content.Server.Atmos.Piping.Unary.EntitySystems;

public sealed partial class GasVentPumpSystem
{
    [Dependency] private OrbitraParticleBurstSystem _orbitraVentEffects = default!;

    private void OrbitraVentParticles(EntityUid uid, GasMixture moved, bool intake, GasMixture? room = null)
    {
        if (moved.TotalMoles > 0.01f &&
            (_orbitraVentEffects.VisibleGasMoles(moved) >= 0.01f || _orbitraVentEffects.VisibleGasMoles(room) >= 0.25f))
            _orbitraVentEffects.EmitVentFlow(uid, intake);
    }
}

public sealed partial class GasVentScrubberSystem
{
    [Dependency] private OrbitraParticleBurstSystem _orbitraVentEffects = default!;
}
