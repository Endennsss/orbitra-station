// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Shared.Materials;

public sealed partial class MaterialPrototype
{
    /// <summary>
    /// Целое число единиц материала, расходуемых принтером после скидок.
    /// </summary>
    [DataField]
    public int OrbitraLatheUnit = 1;
}
