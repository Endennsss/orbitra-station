// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared._Orbitra.Xenobiology;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Xenobiology;

/// <summary>Многоразовый промышленный экстракт.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologyIndustrialComponent : Component
{
    /// <summary>Порода индукции, расход топлива, время цикла и продукты.</summary>
    [DataField(required: true)]
    public ProtoId<OrbitraXenobiologyIndustrialRecipePrototype> Recipe;

    /// <summary>Пополняемый раствор для жидкой плазмы.</summary>
    [DataField]
    public string Solution = "extract";

    /// <summary>Защищает от повторного запуска партии во вложенных событиях химии.</summary>
    public bool Producing;
}

/// <summary>Отмечает заправленные промышленные экстракты для проверки положения и цикла.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class OrbitraXenobiologyIndustrialActiveComponent : Component
{
    /// <summary>Когда заправленный экстракт без активного цикла снова проверит своё положение.</summary>
    public TimeSpan NextIdleCheck;
    /// <summary>Срок завершения следующей партии. Сбрасывается при прерывании цикла.</summary>
    [AutoPausedField]
    public TimeSpan? NextProduction;
}

/// <summary>Учитывает экстракты, скормленные индуцированному взрослому, и блокирует другие изменения его жизненного цикла.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologyIndustrialInductionComponent : Component
{
    /// <summary>Рецепт, выбранный при расходовании катализатора.</summary>
    public ProtoId<OrbitraXenobiologyIndustrialRecipePrototype> Recipe;

    /// <summary>Количество уже внесённых неиспользованных экстрактов той же породы.</summary>
    public int ExtractsFed;

    /// <summary>Резервирует итоговое превращение до спавна или удаления сущностей.</summary>
    public bool Consumed;
}
