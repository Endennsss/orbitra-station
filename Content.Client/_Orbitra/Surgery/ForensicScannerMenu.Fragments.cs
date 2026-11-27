using System.Linq;
using Content.Shared.Forensics.Components;

namespace Content.Client.Forensics;

public sealed partial class ForensicScannerMenu
{
    private void UpdateFragmentHint(ForensicScannerComponent scanner)
    {
        var zones = scanner.OrbitraFragmentZones;
        EvidenceHint.Visible = zones != null;
        if (zones == null)
            return;
        if (zones.Length == 0)
        {
            EvidenceHint.SetMessage(Loc.GetString("orbitra-forensic-body-clear"));
            return;
        }

        var names = zones.Select(zone => Loc.GetString($"orbitra-surgery-zone-{zone.ToString().ToLowerInvariant()}"));
        EvidenceHint.SetMessage(Loc.GetString("orbitra-forensic-body-fragments", ("zones", string.Join(", ", names))));
    }
}
