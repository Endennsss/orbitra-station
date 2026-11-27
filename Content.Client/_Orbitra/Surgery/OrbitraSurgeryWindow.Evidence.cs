using Content.Shared._Orbitra.Surgery;

namespace Content.Client._Orbitra.Surgery;

public sealed partial class OrbitraSurgeryWindow
{
    private void UpdateEvidenceHint()
    {
        var open = _zone is { } region && _state?.GetAccess(region) == SurgeryAccess.Open;
        EvidenceHint.Visible = _zone is { } selected && (InjuryAt(selected) & SurgeryInjury.Shrapnel) != 0 &&
            (_procedure == null || open && _procedure == SurgeryProcedure.OpenAccess) && _state?.ActiveProcedure == null;
        if (EvidenceHint.Visible)
        {
            var key = open ? "orbitra-surgery-fragment-ready" : "orbitra-surgery-fragment-access";
            EvidenceHint.SetMessage(_loc.GetString(key));
        }
    }
}
