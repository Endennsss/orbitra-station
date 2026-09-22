using System.Numerics;
using Content.Shared._Orbitra.Particles;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Wall;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._Orbitra.Particles;

public sealed partial class OrbitraParticleBurstSystem
{
    /// <summary>Captures coordinates before damage can destroy the target.</summary>
    public BulletImpact? CaptureBulletImpact(EntityUid target, EntityUid projectile, bool ballistic, Vector2? contactPoint = null)
    {
        if (TerminatingOrDeleted(target) || TerminatingOrDeleted(projectile) ||
            Transform(target).MapUid == null || _container.IsEntityInContainer(target))
            return null;

        // Снаряд находится со стороны контакта; положение стрелка после рикошета не подходит.
        var point = _particles.Contact(target, projectile, out var direction);
        var incoming = _physics.GetMapLinearVelocity(projectile) - _physics.GetMapLinearVelocity(target);
        if (incoming.LengthSquared() > 0.0001f)
            direction = MathF.Atan2(-incoming.Y, -incoming.X);
        var xform = Transform(target);
        if (contactPoint is { } contact)
            point = _transform.ToCoordinates(xform.GridUid ?? xform.MapUid!.Value, new MapCoordinates(contact, xform.MapID));
        if (TryComp<BloodstreamComponent>(target, out var blood))
        {
            if (blood.BloodSolution is not { } solution || solution.Comp.Solution.Volume <= 0)
                return null;
            var reference = blood.BloodReferenceSolution;
            var color = reference.GetColor(_prototype);
            color.A = 1;
            return new(GetNetEntity(target), point, "OrbitraParticleBlood", direction, color, false);
        }

        if (!ballistic || HasComp<MobStateComponent>(target))
            return null;
        var material = TryComp<OrbitraParticleMaterialComponent>(target, out var surface)
            ? surface.Material : OrbitraParticleMaterial.Generic;
        var mark = xform.Anchored && xform.GridUid != null &&
            (surface != null || HasComp<WallComponent>(target));
        return new(GetNetEntity(target), point, OrbitraBallistics.Effect(material), direction, null, mark);
    }

    /// <summary>Publishes only impacts which actually caused physical damage.</summary>
    public bool TryBulletImpact(BulletImpact? impact, DamageSpecifier damage)
    {
        if (!CanBulletImpact(impact, damage))
            return false;
        var hit = impact!.Value;
        RaiseNetworkEvent(new OrbitraParticleBurstEvent(hit.Source, GetNetCoordinates(hit.Point),
            hit.Effect, hit.Direction, hit.Tint, hit.Mark), Filter.Pvs(hit.Point, entityMan: EntityManager));
        return true;
    }

    private static bool CanBulletImpact(BulletImpact? impact, DamageSpecifier damage)
    {
        if (impact == null)
            return false;
        foreach (var (type, amount) in damage.DamageDict)
            if (type.Id is "Blunt" or "Slash" or "Piercing" && amount > 0)
                return true;
        return false;
    }

    public readonly record struct BulletImpact(NetEntity Source, EntityCoordinates Point, string Effect,
        float Direction, Color? Tint, bool Mark);
}
