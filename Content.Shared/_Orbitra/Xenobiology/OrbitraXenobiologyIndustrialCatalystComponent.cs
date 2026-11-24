// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Shared._Orbitra.Xenobiology;

/// <summary>Одноразовый печатный катализатор исследования для промышленной индукции слизня.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologyIndustrialCatalystComponent : Component
{
    /// <summary>Резервирует катализатор до событий компонентов и удаления.</summary>
    public bool Used;
}
