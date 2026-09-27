using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Server-owned manifestation; an unbound admin spawn has no destructive effects.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarManifestationComponent : Component
{
    /// <summary>Explicit, already supported territory recipes used by the manifestation.</summary>
    [DataField] public EntProtoId RecipeSource = "OrbitraRatvarFabricator";
    /// <summary>Maximum conversion distance in grid tiles.</summary>
    [DataField] public int Radius = 12;
    /// <summary>Maximum tiles processed per update batch.</summary>
    [DataField] public int TileBudget = 16;
    /// <summary>Delay between bounded territory updates.</summary>
    [DataField] public TimeSpan Interval = TimeSpan.FromSeconds(0.1);
    /// <summary>Delay between autonomous roaming steps.</summary>
    [DataField] public TimeSpan MoveInterval = TimeSpan.FromSeconds(1);
    /// <summary>Range of the native gravitational pulse in world metres.</summary>
    [DataField] public float PullRange = 10;
    /// <summary>Native gravitational impulse coefficient per roaming update.</summary>
    [DataField] public float PullVelocity = 2;
    /// <summary>Winning rule which exclusively owns this manifestation.</summary>
    public EntityUid? Rule;
    /// <summary>Original station grid; moving the entity elsewhere suspends its effects.</summary>
    public EntityUid? Grid;
    /// <summary>Original map, checked again before every batch.</summary>
    public EntityUid? Map;
    /// <summary>Earliest next territory batch.</summary>
    public TimeSpan NextUpdate;
    /// <summary>Earliest next roaming step.</summary>
    public TimeSpan NextMove;
    /// <summary>Cursor through a finite square around the current position.</summary>
    public int TileCursor;
}
