// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared.Materials;

namespace Content.Shared.Lathe;

public abstract partial class SharedLatheSystem
{
    /// <summary>
    /// Applies material discounts and rounds the cost up to whole material units.
    /// </summary>
    public static int AdjustMaterial(MaterialPrototype material, int original, bool reduce, float multiplier)
    {
        var adjustedAmount = AdjustMaterial(original, reduce, multiplier);
        var unit = material.OrbitraLatheUnit;
        if (unit <= 1 || original <= 0)
            return adjustedAmount;

        return Math.Max(unit, (int) Math.Ceiling(adjustedAmount / (double) unit) * unit);
    }
}
