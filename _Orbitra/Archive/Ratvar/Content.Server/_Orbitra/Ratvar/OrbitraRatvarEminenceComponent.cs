using Robust.Shared.Player;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Server-only reservation on a mind. This does not grant membership, vision or body control.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarEminenceComponent : Component
{
    /// <summary>Cult whose unique observer slot is reserved.</summary>
    public EntityUid Rule;
    /// <summary>Observer body at selection time; transferring the mind invalidates the selection.</summary>
    public EntityUid? ObserverBody;
    /// <summary>Selected body, never an authority to interact or expand PVS on its own.</summary>
    public EntityUid? Target;
    /// <summary>Target's mind at selection time, preventing silent substitution of another player.</summary>
    public EntityUid? TargetMind;
    /// <summary>Session owning the native camera subscription, retained for cleanup after detach.</summary>
    public ICommonSession? ViewSession;
    /// <summary>Successful linked recall cooldown, preserved across reconnection.</summary>
    public TimeSpan RecallReadyAt;
    /// <summary>True while a target channels a linked recall.</summary>
    public bool Recalling;
    /// <summary>Invalidates old do-after callbacks when observation changes.</summary>
    public uint RecallSequence;
    /// <summary>Pending one-shot mass recall deadline.</summary>
    public TimeSpan? MassRecallAt;
    /// <summary>Explicitly selected waygate for the pending mass recall.</summary>
    public EntityUid? MassRecallDestination;
    /// <summary>Mind-bound manipulation cooldown.</summary>
    public TimeSpan RealityReadyAt;
}
