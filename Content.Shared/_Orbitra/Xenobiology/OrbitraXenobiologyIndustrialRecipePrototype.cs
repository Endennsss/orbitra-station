// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Xenobiology;

/// <summary>Требования индукции и одна ограниченная промышленная партия на плазме.</summary>
[Prototype]
public sealed partial class OrbitraXenobiologyIndustrialRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Суффикс для определения содержимого Orbitra в инструментах.</summary>
    [DataField]
    public string Suffix = "Orbitra";

    /// <summary>Порода слизня и обычных экстрактов, принимаемых этим рецептом.</summary>
    [DataField(required: true)]
    public ProtoId<OrbitraXenobiologyBreedPrototype> Breed;

    /// <summary>Промышленный экстракт, получаемый после индукции.</summary>
    [DataField(required: true)]
    public EntProtoId Extract;

    /// <summary>Количество неиспользованных обычных экстрактов, расходуемых после катализатора.</summary>
    [DataField]
    public int RequiredExtracts = 6;

    /// <summary>Жидкая плазма, расходуемая перед выдачей каждой партии.</summary>
    [DataField]
    public FixedPoint2 PlasmaCost = FixedPoint2.New(5);

    /// <summary>Время непрерывного нахождения на полу для получения партии.</summary>
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(60);

    /// <summary>Ограниченный набор штатных или локальных товаров одной партии.</summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> Products = new();
}
