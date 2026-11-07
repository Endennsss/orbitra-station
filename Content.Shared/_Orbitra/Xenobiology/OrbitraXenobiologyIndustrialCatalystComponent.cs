// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Shared._Orbitra.Xenobiology;

/// <summary>A printed, single-use research gate for industrial slime induction.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologyIndustrialCatalystComponent : Component
{
    /// <summary>Claims the catalyst before any component or deletion callbacks.</summary>
    public bool Used;
}
