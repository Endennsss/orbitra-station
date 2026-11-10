using Content.Shared.Atmos;

namespace Content.Shared._Orbitra.Atmos;

public static class OrbitraAtmosVisual
{
    public static byte Pack(byte stage, byte tint, AtmosDirection flow) =>
        (byte) ((stage & 3) | ((stage == 0 ? 0 : tint & 7) << 2) | (FlowIndex(flow) << 5));

    public static byte FireStage(byte visual) => (byte) (visual & 3);
    public static byte Tint(byte visual) => (byte) ((visual >> 2) & 7);

    public static byte FlowIndex(AtmosDirection flow) => flow switch
    {
        AtmosDirection.North => 1,
        AtmosDirection.South => 2,
        AtmosDirection.East => 3,
        AtmosDirection.West => 4,
        _ => 0,
    };

    public static AtmosDirection Flow(byte visual) => (visual >> 5) switch
    {
        1 => AtmosDirection.North,
        2 => AtmosDirection.South,
        3 => AtmosDirection.East,
        4 => AtmosDirection.West,
        _ => AtmosDirection.Invalid,
    };

    public static byte FireTint(float plasmaHeat, float tritiumHeat)
    {
        var total = plasmaHeat + tritiumHeat;
        return total > 0f && float.IsFinite(total) ? (byte) (1 + MathF.Round(6f * (tritiumHeat / total))) : (byte) 0;
    }
}
