// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Xenobiology;

/// <summary>Наследуемые признаки и стадия развития лабораторного слизня.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologySlimeComponent : Component
{
    /// <summary>Порода, определяющая варианты мутаций, рост и извлечение экстрактов.</summary>
    [DataField]
    public ProtoId<OrbitraXenobiologyBreedPrototype> Breed = "OrbitraGrey";

    /// <summary>Достиг ли образец зрелости.</summary>
    [DataField]
    public bool Adult;

    /// <summary>Накопленное время роста при достаточном питании и подходящей температуре.</summary>
    [ViewVariables]
    public float Growth;

    /// <summary>Поглощённая пища, доступная для роста или деления.</summary>
    [DataField]
    public float Biomass;

    /// <summary>Количество делений от исходной культуры до этого образца.</summary>
    [DataField]
    public int Generation;

    /// <summary>Наследуемая вероятность смены породы потомком до учёта питания и температуры.</summary>
    [DataField]
    public float MutationChance = 0.25f;

    /// <summary>Количество детёнышей при делении.</summary>
    [DataField]
    public int Offspring = 2;

    /// <summary>Количество экстрактов из трупа этого взрослого.</summary>
    [DataField]
    public int ExtractYield = 1;

    /// <summary>Наследуемый множитель скорости взросления.</summary>
    [DataField]
    public float GrowthRate = 1f;

    /// <summary>Наследуемая устойчивость к агрессии от голода. При значении 1 слизень остаётся ручным.</summary>
    [DataField]
    public float Docility;

    /// <summary>Наследуемое расширение диапазона комфортной температуры в кельвинах.</summary>
    [DataField]
    public float ThermalTolerance;

    /// <summary>Общий одноразовый флаг деления и извлечения из трупа.</summary>
    [ViewVariables]
    public bool Consumed;

    /// <summary>Приостановлены ли развитие и поведение NPC в контейнере.</summary>
    [ViewVariables]
    public bool Suspended;

    /// <summary>Перевёл ли голод образец во враждебную фракцию.</summary>
    [ViewVariables]
    public bool Hostile;

    /// <summary>Оставшееся время, пока недавняя еда подавляет агрессию от голода.</summary>
    [ViewVariables]
    public float FeedingGrace;

    /// <summary>Белок, съеденный образцом за всю жизнь.</summary>
    [ViewVariables]
    public float ProteinIntake;

    /// <summary>Растительные питательные вещества, съеденные образцом за всю жизнь.</summary>
    [ViewVariables]
    public float NutrimentIntake;

    /// <summary>Время в прохладных условиях, влияющее на выбор мутации.</summary>
    [ViewVariables]
    public float ColdExposure;

    /// <summary>Время в тёплых условиях, влияющее на выбор мутации.</summary>
    [ViewVariables]
    public float WarmExposure;

    /// <summary>Приостановил ли контейнер выполнявшееся поведение HTN.</summary>
    [ViewVariables]
    public bool ResumeHtn;
}

/// <summary>Завершает ручное деление, когда игрок остаётся рядом с образцом.</summary>
[Serializable, NetSerializable]
public sealed partial class OrbitraXenobiologyDivideDoAfterEvent : SimpleDoAfterEvent;
