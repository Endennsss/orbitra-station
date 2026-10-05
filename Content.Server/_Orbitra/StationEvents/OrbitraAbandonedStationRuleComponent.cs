using Content.Shared.AlertLevel;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.StationEvents;

/// <summary>
/// Configuration and round-local state of the loss of Central Command communications.
/// All durations are measured in simulation time.
/// </summary>
[RegisterComponent]
public sealed partial class OrbitraAbandonedStationRuleComponent : Component
{
    /// <summary>Delay between the initial warning and suspension of services.</summary>
    [DataField]
    public TimeSpan WarningDuration = TimeSpan.FromMinutes(1);

    /// <summary>Earliest first recovery check, measured from round start.</summary>
    [DataField]
    public TimeSpan RecoveryMinimum = TimeSpan.FromMinutes(90);

    /// <summary>Latest first recovery check, measured from round start.</summary>
    [DataField]
    public TimeSpan RecoveryMaximum = TimeSpan.FromMinutes(120);

    /// <summary>Delay after a failed check or activation past the chosen recovery time.</summary>
    [DataField]
    public TimeSpan RetryInterval = TimeSpan.FromMinutes(15);

    /// <summary>Independent success probability of each recovery check, from zero to one.</summary>
    [DataField]
    public float RecoveryChance = 0.5f;

    [DataField]
    public ProtoId<AlertLevelPrototype> IsolationAlert = "OrbitraIsolation";

    [DataField]
    public SoundSpecifier? WarningSound;

    [DataField]
    public SoundSpecifier? IsolationSound;

    // Сохраняем только уровни, которые заменило само событие.
    public Dictionary<EntityUid, ProtoId<AlertLevelPrototype>> PreviousAlerts = new();

    [ViewVariables] public OrbitraAbandonedStationPhase Phase;
    [ViewVariables] public TimeSpan IsolationAt;
    [ViewVariables] public TimeSpan RecoveryAt;
    [ViewVariables] public int RecoveryAttempts;
}

public enum OrbitraAbandonedStationPhase : byte
{
    Pending,
    Warning,
    Isolated,
    Finished,
}
