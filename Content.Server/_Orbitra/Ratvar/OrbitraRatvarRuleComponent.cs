using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>One cult's authoritative economy, progression and round outcome.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarRuleComponent : Component
{
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
    /// <summary>Minimum delay between messages in the cult's collective mind.</summary>
    [DataField] public TimeSpan MessageCooldown = TimeSpan.FromSeconds(2);
    public int Energy;
    public int Generated;
    public TimeSpan StartedAt;
    public TimeSpan NextUpdate;
    public TimeSpan? SummonAt;
    public TimeSpan? FinishAt;
    public EntityUid? Ark;
    public EntityUid? Station;
    public bool Won;
    public bool Lost;
    public readonly HashSet<EntityUid> Members = [];
    public readonly HashSet<EntityUid> Converted = [];
    public readonly Dictionary<EntityUid, TimeSpan> HolyWaterSince = [];
    public readonly Dictionary<EntityUid, TimeSpan> ProtectedUntil = [];
}
