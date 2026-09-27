namespace Content.Server._Orbitra.Ratvar;

/// <summary>A named, destructible endpoint in one cult's station-local network.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarTravelComponent : Component
{
    /// <summary>Station travel adaptation; no Reebe destination is implied.</summary>
    [DataField] public TimeSpan Delay = TimeSpan.FromSeconds(2.5);
    /// <summary>Minimum time between successful journeys from this endpoint.</summary>
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(3);
    /// <summary>Player-supplied plain text, never interpreted as markup.</summary>
    public string Label = string.Empty;
    public TimeSpan NextUse;
}
