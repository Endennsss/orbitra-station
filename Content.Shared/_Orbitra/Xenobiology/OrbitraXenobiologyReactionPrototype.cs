// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Xenobiology;

/// <summary>Одноразовый рецепт экстракта из каталога реакций лаборатории.</summary>
[Prototype]
public sealed partial class OrbitraXenobiologyReactionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Порода, экстракт которой даёт эту реакцию.</summary>
    [DataField(required: true)]
    public ProtoId<OrbitraXenobiologyBreedPrototype> Breed;

    /// <summary>Локализованное название продукта.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Локализованное описание результата и способа обращения.</summary>
    [DataField(required: true)]
    public LocId Description;

    /// <summary>Суффикс для определения содержимого Orbitra в редакторе.</summary>
    [DataField]
    public string Suffix = "Orbitra";

    /// <summary>Реагент, расходуемый для запуска реакции.</summary>
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    /// <summary>Требуемое количество катализатора. Избыток не увеличивает выход.</summary>
    [DataField]
    public FixedPoint2 Amount = FixedPoint2.New(5);

    /// <summary>Ограниченное количество каждого продукта.</summary>
    [DataField]
    public Dictionary<EntProtoId, int> Products = new();

    /// <summary>Реагенты, выдаваемые в отдельной пробирке для образца.</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Reagents = new();

    /// <summary>Отложенный эффект, дополняющий выдачу предметов и реагентов.</summary>
    [DataField]
    public OrbitraXenobiologyReactionEffect Effect;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);

    [DataField]
    public float Radius = 2f;

    /// <summary>Стаки огня, понижение температуры в K, энергия ЭМИ или интенсивность взрыва.</summary>
    [DataField]
    public float Strength;

    /// <summary>Длительность отключения ЭМИ или стазиса.</summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(3);

    [DataField]
    public List<EntProtoId> Creatures = new();

    [DataField]
    public int SpawnCount = 1;
}

public enum OrbitraXenobiologyReactionEffect : byte
{
    None,
    Fire,
    Cold,
    Emp,
    Explosion,
    Spawn,
    Stasis,
}
