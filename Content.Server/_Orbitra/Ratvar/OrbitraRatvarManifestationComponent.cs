using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using System.Numerics;

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
    [DataField] public TimeSpan MoveInterval = TimeSpan.FromSeconds(0.1);
    /// <summary>Roaming speed in grid units per second.</summary>
    [DataField] public float MoveSpeed = 1.5f;
    /// <summary>Persistent roaming heading, independent of sprite rotation.</summary>
    public Vector2 Heading;
    /// <summary>Next autonomous heading choice.</summary>
    public TimeSpan NextTurn;
    /// <summary>Gravity pulse deadline, independent of movement frequency.</summary>
    public TimeSpan NextPull;
    /// <summary>Existing attributed soundtrack; never imported or relicensed here.</summary>
    [DataField] public SoundSpecifier Music = new SoundPathSpecifier("/Audio/Lobby/endless_space.ogg");
    /// <summary>Owned looping audio, stopped when the manifestation is inactive or deleted.</summary>
    public EntityUid? MusicStream;
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
    /// <summary>Explicit administrator test deadline, independent of cult victory.</summary>
    public TimeSpan? PreviewUntil;
}
