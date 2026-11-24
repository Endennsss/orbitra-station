// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Research.Bluespace;

/// <summary>Одна партия наград с весом выпадения в выбираемой категории.</summary>
[Prototype]
public sealed partial class OrbitraBluespaceHarvesterLootPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Метаданные форка для таблицы наград.</summary>
    [DataField]
    public string Suffix = "Orbitra";

    /// <summary>Категория партии, которую может выбрать оператор.</summary>
    [DataField(required: true)]
    public OrbitraBluespaceLootCategory Category;

    /// <summary>Относительный вес среди допустимых партий той же категории.</summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>Прототип и число единиц с разбиением на штатные стопки. До 300 единиц и 16 сущностей на партию.</summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> Products = new();
}
