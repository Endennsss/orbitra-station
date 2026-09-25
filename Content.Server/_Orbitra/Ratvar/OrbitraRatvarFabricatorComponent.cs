using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Fabricator repair settings and the current operation.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarFabricatorComponent : Component
{
    /// <summary>Shared cult energy charged on successful completion.</summary>
    [DataField]
    public int Energy = 200;

    /// <summary>Required uninterrupted repair time.</summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(6);

    /// <summary>Extra repair amount added to the remaining integrity.</summary>
    [DataField]
    public int RepairBonus = 15;

    /// <summary>Exact neutral structure prototypes allowed as repair targets.</summary>
    [DataField]
    public HashSet<EntProtoId> NeutralStructures = new();

    /// <summary>Prevents concurrent use of the same tool.</summary>
    public DoAfterId? Pending;
}
