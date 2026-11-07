// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Shared._Orbitra.Xenobiology;

/// <summary>The inherited trait affected by a xenobiological gene treatment.</summary>
public enum OrbitraXenobiologyTreatment : byte
{
    Mutation,
    Stability,
    Growth,
    Yield,
    Fertility,
    Docility,
    Thermal,
}

/// <summary>A single-use treatment applied directly to a laboratory slime.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologySerumComponent : Component
{
    /// <summary>The bounded, heritable genetic change attempted by this treatment.</summary>
    [DataField(required: true)]
    public OrbitraXenobiologyTreatment Treatment;

    /// <summary>Whether an application already consumed this treatment.</summary>
    public bool Used;
}
