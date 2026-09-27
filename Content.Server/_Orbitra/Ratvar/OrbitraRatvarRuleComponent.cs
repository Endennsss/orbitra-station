using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>One cult's authoritative economy, progression and round outcome.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarRuleComponent : Component
{
    /// <summary>Admin-only tier override; null preserves normal progression and participant requirements.</summary>
    public int? TestTier;
    /// <summary>Biological resource shared only by this cult, never convertible into electricity.</summary>
    public Content.Shared.FixedPoint.FixedPoint2 Vitality;
    /// <summary>Maximum stored biological resource.</summary>
    [DataField] public Content.Shared.FixedPoint.FixedPoint2 VitalityCapacity = 10000;
    [DataField] public int StartingEnergy = 200;
    [DataField] public int MaxEnergy = 10000;
    [DataField] public int TierTwoEnergy = 600;
    [DataField] public int TierThreeEnergy = 1800;
    [DataField] public int TierTwoConverts = 2;
    [DataField] public int TierThreeConverts = 4;
    [DataField] public TimeSpan ConversionDelay = TimeSpan.FromSeconds(5);
    [DataField] public TimeSpan PurificationDelay = TimeSpan.FromSeconds(30);
    [DataField] public ProtoId<ReagentPrototype> PurifyingReagent = "Holywater";
    [DataField] public TimeSpan ConversionImmunity = TimeSpan.FromMinutes(2);
    [DataField] public TimeSpan EarliestArk = TimeSpan.FromMinutes(20);
    [DataField] public TimeSpan ArkDefence = TimeSpan.FromMinutes(5);
    [DataField] public int ArkEnergy = 2000;
    [DataField] public int ArkCultists = 3;
    [DataField] public float RitualRange = 1.5f;
    [DataField] public float ArkSupportRange = 5f;
    [DataField] public int MaxMarauders = 2;
    /// <summary>Living builders and unoccupied builder shells reserve the same limit.</summary>
    [DataField] public int MaxCogscarabs = 2;
    /// <summary>Minimum delay between messages in the cult's collective mind.</summary>
    [DataField] public TimeSpan MessageCooldown = TimeSpan.FromSeconds(2);
    public int Energy;
    public int Generated;
    /// <summary>Observed income and expenditure per second, not a forecast.</summary>
    public float IncomeRate;
    public float ExpenseRate;
    public int SampledEnergy;
    public int SampledGenerated;
    public TimeSpan SampledAt;
    public TimeSpan StartedAt;
    public TimeSpan NextUpdate;
    public TimeSpan? SummonAt;
    public TimeSpan? FinishAt;
    /// <summary>Duration of the station-local manifestation after victory is irrevocably locked.</summary>
    [DataField] public TimeSpan FinaleDuration = TimeSpan.FromSeconds(30);
    /// <summary>Delay before the single final ignition pulse.</summary>
    [DataField] public TimeSpan FinalePulseDelay = TimeSpan.FromSeconds(25);
    /// <summary>Finite native fire stacks used instead of Bee's infinite fire value.</summary>
    [DataField] public float FinaleFireStacks = 10;
    /// <summary>One-shot pulse deadline; cleared before any gameplay side effects.</summary>
    public TimeSpan? FinalePulseAt;
    /// <summary>Unique manifestation spawned by the successful defence.</summary>
    public EntityUid? Manifestation;
    public EntityUid? Ark;
    public EntityUid? Station;
    public bool Won;
    public bool Lost;
    public readonly HashSet<EntityUid> Members = [];
    /// <summary>Индекс печатей передачи, не общий список всех построек станции.</summary>
    public readonly HashSet<EntityUid> TransmissionSigils = [];
    public readonly HashSet<EntityUid> Converted = [];
    public readonly Dictionary<EntityUid, TimeSpan> HolyWaterSince = [];
    public readonly Dictionary<EntityUid, TimeSpan> ProtectedUntil = [];
}
