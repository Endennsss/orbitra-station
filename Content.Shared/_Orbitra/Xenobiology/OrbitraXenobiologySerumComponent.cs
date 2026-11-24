// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Shared._Orbitra.Xenobiology;

/// <summary>Наследуемый признак, изменяемый генетической обработкой.</summary>
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

/// <summary>Одноразовая обработка, применяемая прямо к лабораторному слизню.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologySerumComponent : Component
{
    /// <summary>Наследуемое генетическое изменение в допустимых пределах.</summary>
    [DataField(required: true)]
    public OrbitraXenobiologyTreatment Treatment;

    /// <summary>Признак расходования средства при обработке.</summary>
    public bool Used;
}
