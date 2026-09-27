namespace Content.Server._Orbitra.Ratvar;

/// <summary>Body capability for the restricted observer; spawning it grants no membership or reservation.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarEminenceAvatarComponent : Component
{
    /// <summary>Automatically open once per attachment, without reopening a manually closed window.</summary>
    public bool MenuOpened;

    /// <summary>Shared-energy price of a successful linked recall, an Orbitra adaptation of Bee's cog cost.</summary>
    [DataField] public int RecallEnergy = 100;
    /// <summary>Stationary target channel time.</summary>
    [DataField] public TimeSpan RecallDelay = TimeSpan.FromSeconds(7);
    /// <summary>Mind-bound cooldown after successful recall.</summary>
    [DataField] public TimeSpan RecallCooldown = TimeSpan.FromSeconds(600);
    /// <summary>Warning time before the single mass recall.</summary>
    [DataField] public TimeSpan MassRecallDelay = TimeSpan.FromSeconds(3);
    /// <summary>Maximum distance from the ark to the explicitly selected receiving waygate.</summary>
    [DataField] public float ArkRange = 5;
    /// <summary>Shared-energy adaptation of Bee's five-cog event cost.</summary>
    [DataField] public int RealityEnergy = 500;
    /// <summary>Delay between successful event requests.</summary>
    [DataField] public TimeSpan RealityCooldown = TimeSpan.FromSeconds(600);
}
