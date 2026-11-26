using Content.Server._Orbitra.Surgery;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.MedicalScanner;

namespace Content.Server.Medical.HealthAnalyzer;

public sealed partial class ServerHealthAnalyzerSystem
{
    [Dependency] private OrbitraTraumaSystem _trauma = default!;
    [Dependency] private OrbitraOrganSystem _organs = default!;
    [Dependency] private OrbitraSurgeryAnatomySystem _anatomy = default!;

    private void PopulateOrbitraTrauma(HealthAnalyzerUiState state, EntityUid? target)
    {
        state.OrbitraTrauma = new();
        if (target is not {} patient || !HasComp<OrbitraSurgicalPatientComponent>(patient))
            return;

        state.OrbitraOrgans = _organs.GetConditions(patient);
        foreach (var zone in OrbitraTraumaEffectsSystem.Zones)
        {
            if (_anatomy.SupportsZone(patient, zone))
                state.OrbitraTrauma.Add(zone, _trauma.GetInjuries(patient, zone));
        }
    }
}
