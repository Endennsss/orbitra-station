// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Server.Atmos.Components;
using Content.Shared._Orbitra.Radiation;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost.Components;
using Content.Shared.Inventory;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Maps;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;

namespace Content.Server._Orbitra.Radiation;

/// <summary>Applies background cosmic radiation outside protected interiors.</summary>
public sealed partial class OrbitraCosmicRadiationSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedRoofSystem _roof = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraCosmicRadiationProtectionComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<OrbitraCosmicRadiationProtectionComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("orbitra-cosmic-radiation-protection",
                ("protection", MathF.Round((1f - ValidTransmission(ent.Comp.Transmission)) * 100f))));
    }

    /// <summary>Returns the unshielded cosmic intensity at the entity, including held instruments.</summary>
    public float GetExposure(EntityUid uid)
    {
        if (Deleted(uid) || Terminating(uid) || IsPaused(uid) || HasComp<GhostComponent>(uid)
            || !TryComp<TransformComponent>(uid, out var xform) || xform.MapUid is not { } mapUid
            || TryComp<MapAtmosphereComponent>(mapUid, out var atmosphere) && !atmosphere.Space)
            return 0f;

        if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
        {
            var indices = _transform.GetGridOrMapTilePosition(uid, xform);
            if (TryComp<RoofComponent>(gridUid, out var roof) && _roof.IsRooved((gridUid, grid, roof), indices))
                return 0f;

            // Неявная крыша покрывает пол, но не космические тайлы, включая наружные решётки.
            var tile = _map.GetTileRef(gridUid, grid, indices);
            if (!_turf.IsSpace(tile) && HasComp<ImplicitRoofComponent>(gridUid))
                return 0f;
        }

        var intensity = _cfg.GetCVar(OrbitraCosmicRadiationCVars.Intensity);
        return float.IsFinite(intensity) ? Math.Clamp(intensity, 0f, 10f) : 0f;
    }

    /// <summary>Returns transmission through a suit only when worn with a sealed pressure helmet.</summary>
    public float GetTransmission(EntityUid uid)
    {
        if (!_inventory.TryGetSlotEntity(uid, "outerClothing", out var suit)
            || !TryComp<OrbitraCosmicRadiationProtectionComponent>(suit, out var protection)
            || !_inventory.TryGetSlotEntity(uid, "head", out var helmet)
            || !TryComp<PressureProtectionComponent>(helmet, out var seal)
            || !float.IsFinite(seal.LowPressureMultiplier) || seal.LowPressureMultiplier <= 1f)
            return 1f;

        return ValidTransmission(protection.Transmission);
    }

    /// <summary>Validates exposure without changing health, equipment, or the world.</summary>
    public bool CanApplyExposure(EntityUid uid, float duration, out float intensity)
    {
        intensity = 0f;
        if (!float.IsFinite(duration) || duration <= 0f)
            return false;
        intensity = GetExposure(uid);
        return intensity > 0f;
    }

    /// <summary>Applies cosmic damage once and returns ambient intensity for the existing Geiger counters.</summary>
    public float ApplyExposure(EntityUid uid, float duration)
    {
        if (!CanApplyExposure(uid, duration, out var intensity))
            return 0f;

        if (HasComp<MobStateComponent>(uid) && TryComp<DamageableComponent>(uid, out var damageable))
            ApplyDamage((uid, damageable), intensity, duration);
        return intensity;
    }

    private void ApplyDamage(Entity<DamageableComponent> ent, float intensity, float duration)
    {
        var dose = EnsureComp<OrbitraCosmicRadiationDoseComponent>(ent);
        // Дробный остаток не теряется при коротких тиках и смене скафандра.
        var received = Math.Min(10000d, dose.Remainder + (double) intensity * duration * GetTransmission(ent));
        var amount = FixedPoint2.New(received);
        dose.Remainder = Math.Max(0d, received - amount.Double());
        if (amount <= FixedPoint2.Zero)
            return;
        DamageSpecifier damage = new();
        foreach (var type in ent.Comp.RadiationDamageTypeIDs)
            damage.DamageDict[type] = amount;

        // Врождённая устойчивость сохраняется; обычная броня не подменяет космическую защиту.
        if (_damageable.GetDamageModifierSet(ent.AsNullable()) is { } intrinsic)
            damage = DamageSpecifier.ApplyModifierSet(damage, intrinsic);
        _damageable.ChangeDamage(ent.AsNullable(), damage, ignoreResistances: true, interruptsDoAfters: false);
    }

    private static float ValidTransmission(float value)
    {
        return float.IsFinite(value) && value > 0f && value <= 1f ? value : 1f;
    }
}
