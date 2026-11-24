// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Xenobiology;

/// <summary>Одноразовый экстракт слизня для реакции с заданным количеством катализатора.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologyExtractComponent : Component
{
    /// <summary>Порода, по которой выбирается каталог реакций экстракта.</summary>
    [DataField(required: true)]
    public ProtoId<OrbitraXenobiologyBreedPrototype> Breed;

    /// <summary>Раствор для влитых или введённых шприцем катализаторов.</summary>
    [DataField]
    public string Solution = "extract";

    /// <summary>Признак расходования экстракта в реакции.</summary>
    public bool Used;
}
