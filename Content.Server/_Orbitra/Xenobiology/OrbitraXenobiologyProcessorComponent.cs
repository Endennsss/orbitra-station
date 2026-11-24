// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Server._Orbitra.Xenobiology;

/// <summary>Хранит труп одного взрослого слизня для однократного извлечения на питании.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologyProcessorComponent : Component
{
    /// <summary>Контейнер для трупа.</summary>
    [DataField]
    public string Container = "xeno_input";

    /// <summary>Время обработки при наличии питания в секундах.</summary>
    [DataField]
    public float Duration = 5f;
}

/// <summary>Отмечает обрабатывающие машины для периодических обновлений.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class OrbitraXenobiologyProcessingComponent : Component
{
    /// <summary>Время начала партии с учётом паузы карты.</summary>
    [AutoPausedField]
    public TimeSpan StartedAt;

    /// <summary>Срок завершения партии с учётом паузы карты.</summary>
    [AutoPausedField]
    public TimeSpan FinishAt;
}
