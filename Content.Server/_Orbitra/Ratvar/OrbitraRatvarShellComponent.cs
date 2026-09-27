namespace Content.Server._Orbitra.Ratvar;

/// <summary>Ownership of a scripture-created shell; null means an independent spawned mob.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarShellComponent : Component
{
    public EntityUid? Rule;
    /// <summary>Separates builder and combat shell reservations.</summary>
    [DataField] public bool Builder;
}
