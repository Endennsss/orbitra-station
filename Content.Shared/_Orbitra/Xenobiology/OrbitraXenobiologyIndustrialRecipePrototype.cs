// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Xenobiology;

/// <summary>Defines induction requirements and one bounded, plasma-fueled industrial batch.</summary>
[Prototype]
public sealed partial class OrbitraXenobiologyIndustrialRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>The tooling suffix identifying Orbitra content.</summary>
    [DataField]
    public string Suffix = "Orbitra";

    /// <summary>The only slime and ordinary extract lineage accepted by this recipe.</summary>
    [DataField(required: true)]
    public ProtoId<OrbitraXenobiologyBreedPrototype> Breed;

    /// <summary>The industrial extract created after induction.</summary>
    [DataField(required: true)]
    public EntProtoId Extract;

    /// <summary>The unused ordinary extracts consumed after the induction catalyst.</summary>
    [DataField]
    public int RequiredExtracts = 6;

    /// <summary>Liquid plasma consumed before spawning each batch.</summary>
    [DataField]
    public FixedPoint2 PlasmaCost = FixedPoint2.New(5);

    /// <summary>Uninterrupted time on a real floor needed for a batch.</summary>
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(60);

    /// <summary>Bounded native or local items produced per batch.</summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> Products = new();
}
