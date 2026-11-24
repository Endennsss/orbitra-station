// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Server._Orbitra.Research.Bluespace;

/// <summary>Хранит лимиты вторжений на станции, общие для её установок.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class OrbitraBluespaceHarvesterStationComponent : Component
{
    /// <summary>Когда любой харвестер станции сможет открыть следующий разлом.</summary>
    [AutoPausedField]
    public TimeSpan NextIncident;

    /// <summary>Существа, которые ещё считаются в лимите вторжений станции.</summary>
    public HashSet<EntityUid> Incursions = new();

    /// <summary>Когда харвестер сможет создать следующую штатную аномалию.</summary>
    [AutoPausedField]
    public TimeSpan NextAnomaly;

    /// <summary>Активные аномалии харвестеров до их нейтрализации или удаления.</summary>
    public HashSet<EntityUid> Anomalies = new();

    /// <summary>Резервирует лимит станции во вложенных событиях спавна.</summary>
    public bool Spawning;
}
