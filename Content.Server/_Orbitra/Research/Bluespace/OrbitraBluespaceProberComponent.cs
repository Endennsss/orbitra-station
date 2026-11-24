// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Server._Orbitra.Research.Bluespace;

/// <summary>Настройки питаемого от ЛКП источника очков исследований, повышающего блюспейс-фон.</summary>
[RegisterComponent, Access(typeof(OrbitraBluespaceResearchSystem))]
public sealed partial class OrbitraBluespaceProberComponent : Component
{
    /// <summary>Запрошенный режим работы. Физические условия и доступ исследований проверяются отдельно.</summary>
    [DataField]
    public bool Enabled;

    /// <summary>Очки исследований за секунду работы до учёта фона и населения.</summary>
    [DataField]
    public float PointsPerSecond = 5f;

    /// <summary>Прирост фона станции за секунду работы.</summary>
    [DataField]
    public float BackgroundPerSecond = 0.04f;

    /// <summary>Степень, в которую возводится число активных зондов при делении выработки каждого из них.</summary>
    [DataField]
    public float DiminishingExponent = 0.5f;

    /// <summary>Дополнительный множитель выработки при фоне 100.</summary>
    [DataField]
    public float BackgroundPointMultiplier = 1f;

    internal double PointAccumulator;
    internal float AccrualElapsed;
    internal float PointsPerMinute;
    internal float UiElapsed = 1f;
    internal EntityUid? LastServer;
    internal EntityUid? LastScope;
}
