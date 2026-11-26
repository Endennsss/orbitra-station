using Content.Shared.Body.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared.Body.Systems;

public sealed partial class BloodstreamSystem
{
    /// <summary>Доля настоящей крови относительно нормы пациента, без округления до целого процента.</summary>
    public float? GetOrbitraMonitoredBloodLevel(Entity<BloodstreamComponent?> patient)
    {
        if (!Resolve(patient, ref patient.Comp, false) || patient.Comp.BloodReferenceSolution.Volume == 0 ||
            !_solutionContainer.ResolveSolution(patient.Owner, patient.Comp.BloodSolutionName, ref patient.Comp.BloodSolution, out var contents))
            return null;

        // Донорская кровь с другой ДНК учитывается, введённые лекарства не повышают показание.
        var volume = FixedPoint2.Zero;
        foreach (var (reagent, quantity) in contents.Contents)
        {
            if (patient.Comp.BloodReferenceSolution.ContainsPrototype(reagent.Prototype))
                volume += quantity;
        }

        // Деление FixedPoint2 до перевода во float потеряло бы изменения меньше процента.
        return volume.Float() / patient.Comp.BloodReferenceSolution.Volume.Float();
    }
}
