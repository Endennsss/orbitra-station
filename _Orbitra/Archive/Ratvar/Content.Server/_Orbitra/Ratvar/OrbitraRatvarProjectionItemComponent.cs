using Content.Shared.FixedPoint;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Server-owned lifetime of an item created for a crystal projection.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarProjectionItemComponent : Component
{
    /// <summary>Optional native destruction threshold replacing inherited salvage-producing behaviors.</summary>
    [DataField] public FixedPoint2? BreakDamage;

    /// <summary>The only body allowed to retain this item.</summary>
    public EntityUid Projection;

    /// <summary>Prevents duplicate dissolution during nested container and deletion events.</summary>
    public bool Dissolving;
}
