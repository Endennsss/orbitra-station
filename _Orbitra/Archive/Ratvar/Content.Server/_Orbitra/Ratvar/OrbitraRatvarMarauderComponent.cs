using Content.Shared.DoAfter;

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
    /// <summary>Maximum projectile blocks; repairing restores one charge.</summary>
    [DataField] public int ShieldCapacity = 4;
    /// <summary>Base welding time, before the native tool speed modifier.</summary>
    [DataField] public TimeSpan RepairDuration = TimeSpan.FromSeconds(2.5);
    /// <summary>Maximum total damage removed by one repair.</summary>
    [DataField] public float RepairHealing = 10;
    /// <summary>Native welder fuel spent per completed repair.</summary>
    [DataField] public float RepairFuel = 1;
    /// <summary>Remaining blocks belong to this body, not its occupant.</summary>
    public int ShieldCharges;
    /// <summary>Only one mechanic may repair this body at a time.</summary>
    public DoAfterId? RepairPending;
    public EntityUid? DefenceAction;
    public TimeSpan DefenceUntil;
    public TimeSpan NextDefence;
}

/// <summary>Limits stance updates to bodies currently defending.</summary>
[RegisterComponent]
public sealed partial class ActiveOrbitraRatvarDefenceComponent : Component;
