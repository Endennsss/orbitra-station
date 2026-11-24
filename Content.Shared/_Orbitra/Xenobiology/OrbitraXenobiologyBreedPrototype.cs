// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Xenobiology;

/// <summary>Определяет породу и возможные мутации лабораторного слизня.</summary>
[Prototype]
public sealed partial class OrbitraXenobiologyBreedPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Идентификатор форка рядом с породой в инструментах.</summary>
    [DataField]
    public string Suffix = "Orbitra";

    /// <summary>Локализованное имя для лабораторного оборудования.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Локализованное описание породы.</summary>
    [DataField(required: true)]
    public LocId Description;

    /// <summary>Цвет слизня и его экстракта.</summary>
    [DataField]
    public Color Color = Color.White;

    /// <summary>Расстояние от исходной породы в дереве мутаций.</summary>
    [DataField]
    public int Tier;

    /// <summary>Детёныш, создаваемый при наследовании породы.</summary>
    [DataField(required: true)]
    public EntProtoId Mob;

    /// <summary>Экстракт из обработанного взрослого трупа.</summary>
    [DataField(required: true)]
    public EntProtoId Extract;

    /// <summary>Варианты мутации с весами, независимые от вероятности мутации родителя.</summary>
    [DataField]
    public Dictionary<ProtoId<OrbitraXenobiologyBreedPrototype>, float> Mutations = new();

    /// <summary>Время роста с достаточным питанием до учёта наследуемой скорости развития.</summary>
    [DataField]
    public float GrowthDuration = 180f;

    /// <summary>Питательная биомасса, расходуемая при взрослении.</summary>
    [DataField]
    public float GrowthBiomass = 20f;

    /// <summary>Дополнительная биомасса для деления взрослого.</summary>
    [DataField]
    public float DivisionBiomass = 40f;

    /// <summary>Нижняя комфортная температура тела в кельвинах.</summary>
    [DataField]
    public float MinimumTemperature = 283.15f;

    /// <summary>Верхняя комфортная температура тела в кельвинах.</summary>
    [DataField]
    public float MaximumTemperature = 310.15f;
}

/// <summary>Ключи внешности, передаваемые сервером визуализатору слизня.</summary>
[Serializable, NetSerializable]
public enum OrbitraXenobiologyVisuals : byte
{
    Stage,
}

/// <summary>Слои спрайта лабораторных слизней.</summary>
[Serializable, NetSerializable]
public enum OrbitraXenobiologyLayers : byte
{
    Body,
}

/// <summary>Стадия физического развития лабораторного слизня.</summary>
[Serializable, NetSerializable]
public enum OrbitraXenobiologyStage : byte
{
    Baby,
    Adult,
    DeadBaby,
    DeadAdult,
}
