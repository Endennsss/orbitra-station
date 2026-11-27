using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;

namespace Content.Shared._Orbitra.Surgery;

public sealed partial class OrbitraOrganSystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    /// <summary>За штатный тик почки выводят до 0,25 ед. остатка без эффектов стадии Metabolites.</summary>
    public void FilterKidneyMetabolites(EntityUid organ, EntityUid? body)
    {
        if (body is not { } patient || !TryComp<OrbitraOrganComponent>(organ, out var comp) ||
            comp.Kind != SurgeryOrgan.Kidneys || !HasComp<OrbitraHumanOrganPatientComponent>(patient) ||
            !_solutions.TryGetSolution(patient, BloodstreamComponent.DefaultMetabolitesSolutionName, out var solution, out var contents))
            return;

        foreach (var (reagent, _) in contents.Contents)
        {
            if (!ProtoMan.TryIndex<ReagentPrototype>(reagent.Prototype, out var proto) ||
                proto.Metabolisms?.Metabolisms.ContainsKey("Metabolites") == true)
                continue;

            _solutions.RemoveReagent(solution.Value, reagent, 0.25);
            break;
        }
    }
}
