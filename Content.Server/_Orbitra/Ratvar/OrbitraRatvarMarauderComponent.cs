namespace Content.Server._Orbitra.Ratvar;

/// <summary>Body-owned defence, independent of cult membership and shell ownership.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarMarauderComponent : Component
{
    /// <summary>Duration of the defensive stance.</summary>
    [DataField] public TimeSpan DefenceDuration = TimeSpan.FromSeconds(5);
    /// <summary>Minimum interval between stance activations.</summary>
    [DataField] public TimeSpan DefenceCooldown = TimeSpan.FromSeconds(25);
    /// <summary>Incoming positive damage multiplier during defence.</summary>
    [DataField] public float DefenceMultiplier = 0.5f;
    public EntityUid? DefenceAction;
    public TimeSpan DefenceUntil;
    public TimeSpan NextDefence;
}

/// <summary>Limits stance updates to bodies currently defending.</summary>
[RegisterComponent]
public sealed partial class ActiveOrbitraRatvarDefenceComponent : Component;
