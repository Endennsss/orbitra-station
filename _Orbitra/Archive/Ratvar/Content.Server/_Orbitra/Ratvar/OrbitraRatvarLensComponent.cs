using Content.Shared.Damage;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Configuration of a powered interdiction lens.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarLensComponent : Component
{
    /// <summary>Visible target radius on the same grid.</summary>
    [DataField] public float Radius = 5;
    /// <summary>Time between paid scans; missed pulses are not replayed.</summary>
    [DataField] public TimeSpan Interval = TimeSpan.FromSeconds(2);
    /// <summary>Refreshable movement penalty duration.</summary>
    [DataField] public TimeSpan SlowDuration = TimeSpan.FromSeconds(2.6);
    /// <summary>Native movement speed multiplier, an Orbitra adaptation.</summary>
    [DataField] public float SpeedMultiplier = 0.5f;
    /// <summary>Damage to each visible mech per paid pulse.</summary>
    [DataField] public DamageSpecifier MechDamage = new();
    /// <summary>Native EMP battery drain, in joules.</summary>
    [DataField] public float EmpDrain = 1000;
    /// <summary>Native EMP duration.</summary>
    [DataField] public TimeSpan EmpDuration = TimeSpan.FromSeconds(2);
    /// <summary>Earliest next pulse, including protection against repeated requests.</summary>
    public TimeSpan NextPulse;
}

/// <summary>Marks a lens manually enabled by its owning cult.</summary>
[RegisterComponent]
public sealed partial class ActiveOrbitraRatvarLensComponent : Component;
