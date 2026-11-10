using System.Numerics;
using Content.Shared.Damage.Systems;

namespace Content.Server.Physics.Controllers;

public sealed partial class MoverController
{
    private const float SprintStaminaPerSecond = 5f;
    private const float SprintStaminaUpdateInterval = 0.25f;

    [Dependency] private SharedStaminaSystem _sprintStamina = default!;

    public override void UpdateAfterSolve(bool prediction, float frameTime)
    {
        base.UpdateAfterSolve(prediction, frameTime);
        var staminaModifier = _sprintStamina.UniversalStaminaDamageModifier;
        if (staminaModifier <= 0f)
            return;

        foreach (var (uid, mover) in _moversToUpdate)
        {
            if (mover.OrbitraSprintFraction <= 0f ||
                !PhysicsQuery.TryComp(uid, out var body) ||
                Vector2.Dot(body.LinearVelocity, mover.WishDir) <= 0.05f ||
                !OrbitraMobilityQuery.TryComp(uid, out var mobility) ||
                !OrbitraSprintStaminaQuery.TryComp(uid, out var stamina))
            {
                continue;
            }

            mobility.PendingSprintSeconds += frameTime * mover.OrbitraSprintFraction;
            if (mobility.PendingSprintSeconds < SprintStaminaUpdateInterval)
                continue;

            var reserve = stamina.CritThreshold * OrbitraSprintStaminaLimit - stamina.StaminaDamage;
            var cost = Math.Min(reserve / staminaModifier, mobility.PendingSprintSeconds * SprintStaminaPerSecond);
            mobility.PendingSprintSeconds = 0f;
            if (cost > 0f)
                _sprintStamina.TakeStaminaDamage(uid, cost, stamina, visual: false, ignoreResist: true, log: false);
        }
    }
}
