// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Numerics;
using Content.Shared.Movement.Components;

namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    public static float GetOrbitraSprintWeight(InputMoverComponent mover, bool walking, bool sprinting)
    {
        if (walking)
            return 0f;

        return sprinting ? 1f : mover.OrbitraNormalSprintWeight;
    }

    private static (Vector2 Walk, Vector2 Sprint) GetOrbitraGaitMovement(InputMoverComponent mover, Vector2 direction)
    {
        var sprintWeight = GetOrbitraSprintWeight(mover, mover.Walking, mover.Sprinting);
        return (direction * (1f - sprintWeight), direction * sprintWeight);
    }
}
