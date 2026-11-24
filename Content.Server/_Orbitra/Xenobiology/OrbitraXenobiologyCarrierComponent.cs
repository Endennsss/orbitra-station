// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Server._Orbitra.Xenobiology;

/// <summary>Хранит лабораторных слизней живыми сущностями в контейнере.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologyCarrierComponent : Component
{
    /// <summary>Контейнер для образцов.</summary>
    [DataField]
    public string Container = "xeno_carrier";

    /// <summary>Лимит переносимых образцов.</summary>
    [DataField]
    public int Capacity = 1;

    /// <summary>Время захвата одного доступного образца.</summary>
    [DataField]
    public TimeSpan CaptureDelay = TimeSpan.FromSeconds(2);
}
