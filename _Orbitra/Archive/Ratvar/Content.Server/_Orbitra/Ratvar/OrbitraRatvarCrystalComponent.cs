using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Destination and tunable tether for a single temporary manifestation.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarCrystalComponent : Component
{
    /// <summary>Plain-text destination label, visible only through authorized cult interfaces.</summary>
    [DataField] public string Label = string.Empty;
    /// <summary>Body created without copying the user's inventory or mind role.</summary>
    [DataField] public EntProtoId ProjectionPrototype = "OrbitraRatvarProjection";
    /// <summary>Temporary clothing by inventory slot; never copied from the original body.</summary>
    [DataField] public Dictionary<string, EntProtoId> Equipment = new();
    /// <summary>Temporary items placed in free hands, or dropped beside the projection.</summary>
    [DataField] public List<EntProtoId> HandItems = new();
    /// <summary>Maximum manifestation distance from the destination in metres.</summary>
    [DataField] public float Radius = 5;
    /// <summary>Fraction of positive damage reflected to the original body.</summary>
    [DataField] public float BodyDamageFraction = 0.4f;
    /// <summary>Fraction of positive damage reflected to this crystal.</summary>
    [DataField] public float CrystalDamageFraction = 0.6f;
    /// <summary>Exclusive reservation, cleared before returning the visitor.</summary>
    public EntityUid? Projection;
}

/// <summary>Private server-side ownership of an active visit; not a second cult membership.</summary>
[RegisterComponent]
public sealed partial class ActiveOrbitraRatvarProjectionComponent : Component
{
    /// <summary>Original mind whose owned entity never changes during a visit.</summary>
    public EntityUid Mind;
    /// <summary>Vulnerable physical body left at the source crystal.</summary>
    public EntityUid Body;
    /// <summary>Source crystal whose loss ends the visit.</summary>
    public EntityUid Source;
    /// <summary>Destination crystal reserving this projection.</summary>
    public EntityUid Crystal;
    /// <summary>Cult captured at creation and revalidated while active.</summary>
    public EntityUid Rule;
    /// <summary>Items created for this manifestation, including initial container contents.</summary>
    public HashSet<EntityUid> Equipment = new();
    /// <summary>Reentrancy guard for synchronous mind, damage and deletion events.</summary>
    public bool Ending;
}
