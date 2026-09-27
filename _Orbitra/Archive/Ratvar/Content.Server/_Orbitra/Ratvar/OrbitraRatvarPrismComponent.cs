using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Configuration and captured toxins of a cult-owned prosperity prism.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarPrismComponent : Component
{
    /// <summary>Interval between healing pulses; missed pulses are not replayed.</summary>
    [DataField] public TimeSpan Interval = TimeSpan.FromSeconds(2);
    /// <summary>Healing radius on the device's grid.</summary>
    [DataField] public float Radius = 4;
    /// <summary>Non-brute healing per pulse, expressed as negative damage.</summary>
    [DataField] public DamageSpecifier Healing = new();
    /// <summary>Total brute healing per pulse, distributed across existing injuries.</summary>
    [DataField] public FixedPoint2 BruteHealing = 20;
    /// <summary>Stamina recovery per pulse through the native stamina system.</summary>
    [DataField] public float StaminaHealing = 100;
    /// <summary>Maximum amount extracted per toxin and pulse.</summary>
    [DataField] public FixedPoint2 Extraction = 100;
    /// <summary>Total volume that can be captured without destroying excess reagents.</summary>
    [DataField] public FixedPoint2 Capacity = 1000;
    /// <summary>Native reagent group eligible for extraction.</summary>
    [DataField] public string ToxinGroup = "Toxins";
    /// <summary>Duration of the native chemical smoke on destruction.</summary>
    [DataField] public float SmokeDuration = 10;
    /// <summary>Native smoke spread budget, not a radius in tiles.</summary>
    [DataField] public int SmokeSpread = 9;
    /// <summary>Captured reagents, retaining reagent data and actual extracted quantities.</summary>
    public Solution Captured = new();
    /// <summary>Next allowed pulse, also guarding direct repeated requests.</summary>
    public TimeSpan NextPulse;
    /// <summary>Whether a destruction event has already released the contents.</summary>
    public bool Released;
}

/// <summary>Marks prisms enabled by their owners.</summary>
[RegisterComponent]
public sealed partial class ActiveOrbitraRatvarPrismComponent : Component;
