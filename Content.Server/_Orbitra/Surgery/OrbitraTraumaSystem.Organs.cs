using Content.Shared._Orbitra.Surgery;
using Content.Shared.FixedPoint;

namespace Content.Server._Orbitra.Surgery;

public sealed partial class OrbitraTraumaSystem
{
    [Dependency] private OrbitraOrganSystem _organs = default!;

    /// <summary>Один удар может изменить состояние только одного органа на одну ступень.</summary>
    private void ApplyOrganTrauma(EntityUid patient, FixedPoint2 damage, OrbitraTraumaPrototype profile)
    {
        if (!HasComp<OrbitraHumanOrganPatientComponent>(patient))
            return;

        if (Roll(damage, profile.OrganFailure) &&
            TryInjureOrgan(patient, SurgeryOrganCondition.Damaged, SurgeryOrganCondition.Nonfunctional))
            return;

        if (Roll(damage, profile.OrganInjury))
            TryInjureOrgan(patient, SurgeryOrganCondition.Healthy, SurgeryOrganCondition.Damaged);
    }

    private bool TryInjureOrgan(EntityUid patient, SurgeryOrganCondition from, SurgeryOrganCondition to)
    {
        Span<EntityUid> eligible = stackalloc EntityUid[OrbitraOrganSystem.Kinds.Length];
        var count = 0;
        foreach (var kind in OrbitraOrganSystem.Kinds)
        {
            if (_organs.Installed(patient, kind) is { } uid && _organs.IsHumanOrgan(uid, kind) &&
                Comp<OrbitraOrganComponent>(uid).Condition == from)
                eligible[count++] = uid;
        }

        if (count == 0)
            return false;

        var organ = eligible[_random.Next(count)];
        SetOrganCondition(patient, organ, to);
        return true;
    }

    internal void SetOrganCondition(EntityUid patient, EntityUid organ, SurgeryOrganCondition condition)
    {
        var comp = Comp<OrbitraOrganComponent>(organ);
        comp.Condition = condition;
        Dirty(organ, comp);
        Changed?.Invoke(patient);
    }
}
