using Content.Server._Orbitra.Surgery;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.MedicalScanner;

namespace Content.Server.Medical.HealthAnalyzer;

public sealed partial class ServerHealthAnalyzerSystem
{
    [Dependency] private OrbitraTraumaSystem _trauma = default!;

    private void PopulateOrbitraTrauma(HealthAnalyzerUiState state, EntityUid? target)
    {
        state.OrbitraTrauma = new();
        if (target is not {} patient || !HasComp<OrbitraSurgicalPatientComponent>(patient))
            return;

        foreach (var zone in OrbitraTraumaEffectsSystem.Zones)
        {
            var injury = _trauma.GetInjuries(patient, zone);
            if (injury != SurgeryInjury.None)
                state.OrbitraTrauma.Add(zone, injury);
        }
    }
}
