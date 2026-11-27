using System.Linq;
using Content.Shared._Orbitra.Surgery;

namespace Content.Shared.Forensics.Systems;

public sealed partial class SharedForensicScannerSystem
{
    private SurgeryZone[]? ScanRetainedFragments(EntityUid target)
    {
        if (!HasComp<OrbitraSurgicalPatientComponent>(target))
            return null;
        if (!TryComp<OrbitraTraumaComponent>(target, out var trauma))
            return [];

        return OrbitraTraumaEffectsSystem.Zones
            .Where(zone => (trauma.Injuries.GetValueOrDefault(zone) & SurgeryInjury.Shrapnel) != 0)
            .ToArray();
    }
}
