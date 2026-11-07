// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared._Orbitra.Xenobiology;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Xenobiology;

/// <summary>Одно запланированное срабатывание уже израсходованного экстракта.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class OrbitraXenobiologyReactionComponent : Component
{
    public ProtoId<OrbitraXenobiologyReactionPrototype> Recipe;

    [AutoPausedField]
    public TimeSpan FinishAt;

    public GameTick StartedTick;
    public EntityUid? User;

    /// <summary>Следующее изменение раствора закрывает окно привязки к завершённому переносу.</summary>
    public bool AwaitingTransfer;
}
