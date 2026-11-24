using Content.Shared.AlertLevel;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.StationEvents;

/// <summary>
/// Настройки и состояние потери связи с ЦК в текущем раунде. Все интервалы заданы по времени симуляции.
/// </summary>
[RegisterComponent]
public sealed partial class OrbitraAbandonedStationRuleComponent : Component
{
    /// <summary>Время от первого предупреждения до отключения служб.</summary>
    [DataField]
    public TimeSpan WarningDuration = TimeSpan.FromMinutes(1);

    /// <summary>Минимальное время первой проверки восстановления от начала раунда.</summary>
    [DataField]
    public TimeSpan RecoveryMinimum = TimeSpan.FromMinutes(90);

    /// <summary>Максимальное время первой проверки восстановления от начала раунда.</summary>
    [DataField]
    public TimeSpan RecoveryMaximum = TimeSpan.FromMinutes(120);

    /// <summary>Задержка после неудачной проверки или запуска после выбранного времени восстановления.</summary>
    [DataField]
    public TimeSpan RetryInterval = TimeSpan.FromMinutes(15);

    /// <summary>Независимая вероятность успеха каждой проверки восстановления, от 0 до 1.</summary>
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
