namespace Content.Server._Orbitra.Ratvar;

/// <summary>A destructible station-bound invitation beacon with a cult-wide objection period.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarEminenceSpireComponent : Component
{
    /// <summary>Time during which another interaction cancels the proposed summon.</summary>
    [DataField] public TimeSpan ObjectionPeriod = TimeSpan.FromSeconds(60);
    /// <summary>Time available to ghosts to claim the invitation before retry becomes possible.</summary>
    [DataField] public TimeSpan InvitationDuration = TimeSpan.FromSeconds(30);
    /// <summary>Pending objection deadline; null means idle.</summary>
    public TimeSpan? SummonAt;
    /// <summary>Cult that authorized this attempt, immutable throughout the countdown.</summary>
    public EntityUid? PendingRule;
}
