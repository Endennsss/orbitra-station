using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>A server-bound, single-use ghost invitation reserving one cult's Eminence slot.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarEminenceInvitationComponent : Component
{
    /// <summary>Native ghost-role configuration applied only after a server reserves a cult slot.</summary>
    [DataField(required: true)]
    public ComponentRegistry RoleComponents = new();

    /// <summary>Cult assigned by the trusted creation path, never by spawning the prototype alone.</summary>
    public EntityUid? Rule;

    /// <summary>Prevents a second takeover while native mind creation dispatches synchronous events.</summary>
    public bool Claiming;

    /// <summary>Mind allowed to inherit the pending reservation during acquisition.</summary>
    public EntityUid? ClaimMind;

    /// <summary>Debug invitations expire when the test mode is disabled.</summary>
    public bool TestOnly = true;
    /// <summary>Normal invitations require their original beacon to remain valid.</summary>
    public EntityUid? Spire;
    /// <summary>Deadline of a normal invitation.</summary>
    public TimeSpan? ExpiresAt;
}
