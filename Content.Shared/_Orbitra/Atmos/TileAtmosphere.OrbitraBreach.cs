namespace Content.Shared.Atmos;

public sealed partial class TileAtmosphere
{
    // Храним задержку на выходе потока, общем для всех тайлов по пути к нему.
    public TimeSpan OrbitraNextBreachCue;
    public TimeSpan OrbitraNextBreachVisual;
    public AtmosDirection OrbitraAirflow;
    public float OrbitraPlasmaHeat;
    public float OrbitraTritiumHeat;
    public byte OrbitraFireTint;
}
