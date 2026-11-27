using Content.Shared._Orbitra.Surgery;
using Content.Shared.Body;

namespace Content.Server._Orbitra.Surgery;

public sealed partial class OrbitraSurgerySystem
{
    [Dependency] private OrbitraOrganSystem _organs = default!;

    private bool CanOperateOnOrgan(EntityUid patient, SurgeryOrgan kind, SurgeryProcedure procedure)
    {
        if (!HasComp<OrbitraHumanOrganPatientComponent>(patient) || !Enum.IsDefined(kind) ||
            _anatomy.FindOrgan(patient, OrbitraOrganSystem.ZoneFor(kind).ToString()) is not { } region ||
            !TryComp<ParentOrganComponent>(region, out var parent))
            return false;

        if (_organs.Installed(patient, kind) is not { } organ)
            return procedure == SurgeryProcedure.TransplantOrgan;

        return _organs.IsHumanOrgan(organ, kind) && Comp<OrganComponent>(organ).Body == patient &&
            _relations.HasDirectChild((region, parent), organ) && TryComp<ChildOrganComponent>(organ, out var child) && child.Parent == region &&
            OrbitraOrganSystem.Indicated(procedure, Comp<OrbitraOrganComponent>(organ).Condition);
    }

    private bool MatchesOriginalOrgan(EntityUid patient, OrbitraSurgeryOperationComponent op) =>
        op.Organ is not { } kind || _organs.Installed(patient, kind) == op.OriginalOrgan;

    /// <summary>Контейнер меняется только после успешного последнего шага. Перемещается тот же предмет.</summary>
    private bool TryFinishOrganProcedure(EntityUid patient, SurgeryOrgan kind, SurgeryProcedure procedure, EntityUid tool)
    {
        if (!CanOperateOnOrgan(patient, kind, procedure) ||
            !_containers.TryGetContainer(patient, BodyComponent.ContainerID, out var body))
            return false;

        if (procedure == SurgeryProcedure.TransplantOrgan)
        {
            if (!_organs.IsDonor(tool, kind) || _anatomy.FindOrgan(patient, OrbitraOrganSystem.ZoneFor(kind).ToString()) is not { } region ||
                !_containers.Insert(tool, body))
                return false;

            _relations.Relate(region, tool);
            return true;
        }

        if (_organs.Installed(patient, kind) is not { } organ)
            return false;

        if (procedure == SurgeryProcedure.RepairOrgan)
        {
            _trauma.SetOrganCondition(patient, organ, SurgeryOrganCondition.Healthy);
            return true;
        }

        if (!_containers.Remove(organ, body, destination: Transform(patient).Coordinates))
            return false;

        _relations.Orphan(organ);
        return true;
    }
}
