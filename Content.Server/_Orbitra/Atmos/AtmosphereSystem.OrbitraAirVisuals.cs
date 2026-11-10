using Content.Shared._Orbitra.Atmos;
using Content.Shared.Atmos;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    private const int OrbitraFlowTileLimit = 512;
    private static readonly TimeSpan FlowLifetime = TimeSpan.FromSeconds(0.7);
    private static readonly TimeSpan FlowSweepInterval = TimeSpan.FromSeconds(0.2);
    private readonly Dictionary<TileAtmosphere, TimeSpan> _orbitraActiveAirflow = new();
    private TimeSpan _orbitraFlowNextSweep;

    private void RecordOrbitraAirflow(TileAtmosphere tile)
    {
        if (_orbitraActiveAirflow.Count >= OrbitraFlowTileLimit && !_orbitraActiveAirflow.ContainsKey(tile))
            return;

        _orbitraActiveAirflow[tile] = _gameTiming.CurTime + FlowLifetime;
        if (tile.OrbitraAirflow == tile.PressureDirection)
            return;

        tile.OrbitraAirflow = tile.PressureDirection;
        InvalidateVisuals(tile.GridIndex, tile.GridIndices);
    }

    private void ExpireOrbitraAirflow()
    {
        var now = _gameTiming.CurTime;
        if (_orbitraActiveAirflow.Count == 0 || now < _orbitraFlowNextSweep)
            return;
        _orbitraFlowNextSweep = now + FlowSweepInterval;

        foreach (var (tile, expires) in _orbitraActiveAirflow)
        {
            if (expires > now)
                continue;
            _orbitraActiveAirflow.Remove(tile);
            tile.OrbitraAirflow = AtmosDirection.Invalid;
            if (Exists(tile.GridIndex))
                InvalidateVisuals(tile.GridIndex, tile.GridIndices);
        }
    }

    private static void BeginOrbitraFireSample(TileAtmosphere? tile)
    {
        if (tile == null)
            return;
        tile.OrbitraPlasmaHeat = 0f;
        tile.OrbitraTritiumHeat = 0f;
    }

    private void EndOrbitraFireSample(TileAtmosphere? tile)
    {
        if (tile is not { Hotspot.Valid: true })
            return;

        var tint = OrbitraAtmosVisual.FireTint(tile.OrbitraPlasmaHeat, tile.OrbitraTritiumHeat);
        if (tint == 0 || tint == tile.OrbitraFireTint)
            return;

        tile.OrbitraFireTint = tint;
        InvalidateVisuals(tile.GridIndex, tile.GridIndices);
    }

    internal void AddOrbitraFireHeat(TileAtmosphere? tile, Gas fuel, float heat)
    {
        if (tile == null || heat <= 0f)
            return;
        if (fuel == Gas.Plasma)
            tile.OrbitraPlasmaHeat += heat;
        else if (fuel == Gas.Tritium)
            tile.OrbitraTritiumHeat += heat;
    }
}
