// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared._Orbitra.Research.Bluespace;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Research.Bluespace;

/// <summary>Настройки высоковольтного харвестера и ограниченное накопление энергии.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class OrbitraBluespaceHarvesterComponent : Component
{
    /// <summary>Включил ли оператор харвестер.</summary>
    [DataField]
    public bool Enabled;

    /// <summary>Запрошенная мощность в штатном режиме, в ваттах.</summary>
    [DataField]
    public float NormalPowerDraw = 125_000f;

    /// <summary>Запрошенная мощность в форсированном режиме, в ваттах.</summary>
    [DataField]
    public float OverdrivePowerDraw = 250_000f;

    /// <summary>Фактически полученная энергия, необходимая для одной награды, в джоулях.</summary>
    [DataField]
    public float EnergyPerReward = 7_500_000f;

    /// <summary>Прирост фона станции на мегаджоуль фактического потребления.</summary>
    [DataField]
    public float BackgroundPerMegajoule = 1.2f;

    /// <summary>Дополнительный множитель фона в форсированном режиме.</summary>
    [DataField]
    public float OverdriveBackgroundMultiplier = 2f;

    /// <summary>Порог локальной блокировки устройства вдобавок к общей блокировке станции.</summary>
    [DataField]
    public float CriticalLevel = 95f;

    /// <summary>Для снятия локальной блокировки фон должен опуститься ниже этого порога.</summary>
    [DataField]
    public float RestartLevel = 80f;

    /// <summary>Общая задержка вторжений на станции, от 40 секунд.</summary>
    [DataField]
    public TimeSpan IncidentCooldown = TimeSpan.FromSeconds(40);

    /// <summary>Лимит живых существ вторжений на станцию, до шести.</summary>
    [DataField]
    public int MaxActiveIncursions = 6;

    /// <summary>Число существ в волне выше порога вторжения.</summary>
    [DataField]
    public int IncursionWaveSize = 2;

    /// <summary>Число существ в волне выше порога опасности.</summary>
    [DataField]
    public int DangerousWaveSize = 3;

    /// <summary>Общая задержка аномалий на станции, от двух минут.</summary>
    [DataField]
    public TimeSpan AnomalyCooldown = TimeSpan.FromSeconds(120);

    /// <summary>Лимит активных аномалий харвестеров на станцию, до двух.</summary>
    [DataField]
    public int MaxActiveAnomalies = 2;

    /// <summary>Штатная аномалия выше порога опасности.</summary>
    [DataField]
    public EntProtoId AnomalyPrototype = "AnomalyBluespace";

    /// <summary>Время жизни существа вторжения, до трёх минут.</summary>
    [DataField]
    public TimeSpan IncursionLifetime = TimeSpan.FromSeconds(180);

    /// <summary>Временный визуальный эффект местного вторжения.</summary>
    [DataField]
    public EntProtoId RiftPrototype = "OrbitraBluespaceRift";

    /// <summary>Штатное враждебное существо из местного вторжения.</summary>
    [DataField]
    public EntProtoId HostilePrototype = "MobCarp";

    /// <summary>Выбранный режим потребления энергии.</summary>
    [DataField]
    public OrbitraBluespacePowerMode PowerMode;

    /// <summary>Выбранная категория таблицы наград.</summary>
    [DataField]
    public OrbitraBluespaceLootCategory LootCategory;

    /// <summary>Фактически накопленная энергия с верхней границей в одну награду.</summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public double StoredEnergy;

    /// <summary>Активна ли локальная аварийная блокировка.</summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Critical;

    /// <summary>Ожидает ли готовая награда безопасную клетку для выдачи.</summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool OutputBlocked;

    /// <summary>Защищает заряд от повторного списания во вложенных событиях спавна.</summary>
    public bool Producing;

    /// <summary>Требует свежего обновления электросети после смены запрошенной нагрузки.</summary>
    public bool SkipPowerSample;

    /// <summary>Время следующей проверки выхода и вторжений.</summary>
    [AutoPausedField]
    public TimeSpan NextOutputCheck;

    /// <summary>Время следующего обновления интерфейса.</summary>
    [AutoPausedField]
    public TimeSpan NextUiUpdate;
}
