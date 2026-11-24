// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Server._Orbitra.Xenobiology;

/// <summary>Показывает измерения местной колонии и справочник пород.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologyConsoleComponent : Component
{
    /// <summary>Максимальное расстояние измерения в игровых метрах.</summary>
    [DataField]
    public float Range = 20f;
}
