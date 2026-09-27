using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Continuous presence requirements and vitality exchange parameters for one sigil.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarVitalityComponent : Component
{
    /// <summary>Maximum target distance in metres, on the same grid.</summary>
    [DataField] public float Radius = 0.65f;
    /// <summary>Continuous charge time between effects.</summary>
    [DataField] public TimeSpan Interval = TimeSpan.FromSeconds(2);
    /// <summary>Native damage dealt per drain pulse.</summary>
    [DataField] public DamageSpecifier Drain = new();
    /// <summary>Maximum gain for a pulse against a mind-bearing target.</summary>
    [DataField] public FixedPoint2 MindGain = 40;
    /// <summary>Maximum gain for a pulse against an animal without a mind.</summary>
    [DataField] public FixedPoint2 AnimalGain = 10;
    /// <summary>Maximum healing per eligible group/type in one pulse.</summary>
    [DataField] public FixedPoint2 Healing = 5;
    /// <summary>Vitality cost per point actually healed.</summary>
    [DataField] public float HealingCost = 0.3f;
    /// <summary>Fixed revival charge in addition to damage-based cost.</summary>
    [DataField] public FixedPoint2 ReviveBase = 20;
    /// <summary>Additional vitality per point of positive damage for revival.</summary>
    [DataField] public float ReviveCost = 0.6f;
    /// <summary>Single occupant currently charging the sigil.</summary>
    public EntityUid? Target;
    /// <summary>Original occupant mind, to cancel on body transfers.</summary>
    public EntityUid? Mind;
    /// <summary>Completion time of the current charge.</summary>
    public TimeSpan FinishAt;
    /// <summary>Next bounded local candidate lookup.</summary>
    public TimeSpan NextSearch;
}

/// <summary>Prevents overlapping matrices from multiplying effects on one body.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarVitalityTargetComponent : Component
{
    public TimeSpan NextPulse;
}
