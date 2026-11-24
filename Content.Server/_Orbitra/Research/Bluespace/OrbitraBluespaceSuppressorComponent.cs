// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Server._Orbitra.Research.Bluespace;

/// <summary>Настройки питаемого от ЛКП подавителя, доступного при аварийной блокировке станции.</summary>
[RegisterComponent, Access(typeof(OrbitraBluespaceResearchSystem))]
public sealed partial class OrbitraBluespaceSuppressorComponent : Component
{
    /// <summary>Запрошенный режим работы. Устройство требует питания, крепления и принадлежности к станции.</summary>
    [DataField]
    public bool Enabled;

    /// <summary>Снижение фона станции за секунду работы.</summary>
    [DataField]
    public float SuppressionPerSecond = 0.12f;

    internal float UiElapsed = 1f;
}
