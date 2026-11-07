// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Xenobiology;

/// <summary>A single-use slime extract that reacts with a measured chemical catalyst.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologyExtractComponent : Component
{
    /// <summary>The breed whose reaction catalog this extract uses.</summary>
    [DataField(required: true)]
    public ProtoId<OrbitraXenobiologyBreedPrototype> Breed;

    /// <summary>The solution accepting injected or poured catalysts.</summary>
    [DataField]
    public string Solution = "extract";

    /// <summary>Whether a reaction has already claimed this extract.</summary>
    public bool Used;
}
