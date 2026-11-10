// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Numerics;
using Content.Shared._Orbitra.Movement;
using Content.Shared.Damage.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Standing;

namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    private const float OrbitraSprintSpeedMultiplier = 1.3f;
    protected const float OrbitraSprintStaminaLimit = 0.55f;

    [Dependency] private StandingStateSystem _orbitraStanding = default!;
    [Dependency] protected EntityQuery<OrbitraMobilityComponent> OrbitraMobilityQuery = default!;
    [Dependency] protected EntityQuery<StaminaComponent> OrbitraSprintStaminaQuery = default!;

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

    private Vector2 GetOrbitraSprintAdjustment(Entity<InputMoverComponent> entity, Vector2 direction,
        float walkSpeed, float sprintSpeed)
    {
        if (!OrbitraMobilityQuery.HasComp(entity) || !OrbitraSprintStaminaQuery.TryComp(entity, out var stamina))
            return Vector2.Zero;

        if (stamina.Critical || stamina.StaminaDamage >= stamina.CritThreshold * OrbitraSprintStaminaLimit ||
            _orbitraStanding.IsDown(entity.Owner))
        {
            var normalSpeed = float.Lerp(walkSpeed, sprintSpeed, entity.Comp.OrbitraNormalSprintWeight);
            return direction * (normalSpeed - sprintSpeed);
        }

        entity.Comp.OrbitraSprintFraction = direction.Length();
        return direction * (sprintSpeed * (OrbitraSprintSpeedMultiplier - 1f));
    }
}
