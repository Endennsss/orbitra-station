using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Intrinsic builder equipment, independent of mind membership and shell ownership.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarCogscarabComponent : Component
{
    /// <summary>Issued once when the body is created, never on mind transfer.</summary>
    [DataField] public List<EntProtoId> Tools = [];
}
