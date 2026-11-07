// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Shared.Materials;

public sealed partial class MaterialPrototype
{
    /// <summary>
    /// The indivisible amount of material consumed by a lathe after material discounts.
    /// </summary>
    [DataField]
    public int OrbitraLatheUnit = 1;
}
