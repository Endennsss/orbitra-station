using Content.Shared._Orbitra.Surgery;
using Content.Shared.Body.Components;

namespace Content.Shared.Body.Systems;

public sealed partial class BloodstreamSystem
{
    [Dependency] private OrbitraOrganSystem _orbitraOrgans = default!;

    private void RegulateOrbitraBloodLevel(EntityUid patient, BloodstreamComponent blood)
    {
        var (factor, loss) = _orbitraOrgans.HeartFunction(patient);
        if (loss > 0f)
            TryModifyBloodLevel((patient, blood), -loss);
        else
            TryRegulateBloodLevel((patient, blood), blood.BloodRefreshAmount * factor);
    }
}
