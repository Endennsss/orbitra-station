// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Xenobiology;

/// <summary>A dormant culture that becomes a juvenile slime when hydrated outside a container.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologyCultureComponent : Component
{
    /// <summary>The breed to hatch.</summary>
    [DataField(required: true)]
    public ProtoId<OrbitraXenobiologyBreedPrototype> Breed;

    /// <summary>The solution accepting hydration water.</summary>
    [DataField]
    public string Solution = "culture";

    /// <summary>Whether this culture has already hatched.</summary>
    public bool Used;
}
