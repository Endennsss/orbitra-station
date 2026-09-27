using Content.Shared.DoAfter;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Tracks repairs that must be cancelled before this target loses its transform.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarRepairTargetComponent : Component
{
    /// <summary>Active repair operations targeting this structure.</summary>
    public readonly List<DoAfterId> Pending = new();
}
