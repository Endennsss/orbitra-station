using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarMarauderSystem
{
    // Заряды щита и ремонт тела не зависят от роли или владельца оболочки.
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly ProtoId<ToolQualityPrototype>[] Welding = ["Welding"];
    private static readonly EntProtoId ShieldHitEffect = "OrbitraRatvarShieldHit";
    private static readonly EntProtoId ShieldBreakEffect = "OrbitraRatvarShieldBreak";

    private void InitializeShield()
    {
        SubscribeLocalEvent<OrbitraRatvarMarauderComponent, ProjectileReflectAttemptEvent>(OnProjectile);
        SubscribeLocalEvent<OrbitraRatvarMarauderComponent, InteractUsingEvent>(OnRepair);
        SubscribeLocalEvent<OrbitraRatvarMarauderComponent, OrbitraRatvarMarauderRepairEvent>(OnRepairFinished);
        SubscribeLocalEvent<OrbitraRatvarMarauderComponent, ExaminedEvent>(OnShieldExamine);
        SubscribeLocalEvent<OrbitraRatvarMarauderComponent, ComponentShutdown>(OnShieldShutdown);
        SubscribeLocalEvent<OrbitraRatvarMarauderComponent, MobStateChangedEvent>(OnShieldMobState);
    }

    private void OnProjectile(Entity<OrbitraRatvarMarauderComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        if (!args.Cancelled && TryBlockProjectile(ent, (args.ProjUid, args.Component)))
            args.Cancelled = true;
    }

    private void OnRepair(Entity<OrbitraRatvarMarauderComponent> ent, ref InteractUsingEvent args)
    {
        if (!args.Handled)
            args.Handled = TryStartRepair(ent, args.User, args.Used);
    }

    private void OnRepairFinished(Entity<OrbitraRatvarMarauderComponent> ent, ref OrbitraRatvarMarauderRepairEvent args)
    {
        if (args.Handled || ent.Comp.RepairPending != args.DoAfter.Id)
            return;
        ent.Comp.RepairPending = null;
        args.Handled = true;
        if (args.Cancelled || args.Used is not { } tool || !CanRepair(ent, args.User, tool))
            return;

        ent.Comp.ShieldCharges = Math.Min(ent.Comp.ShieldCapacity, ent.Comp.ShieldCharges + 1);
        _damageable.HealEvenly(ent.Owner, -ent.Comp.RepairHealing, origin: args.User);
        _popup.PopupEntity(Loc.GetString("orbitra-ratvar-marauder-repaired",
            ("charges", ent.Comp.ShieldCharges), ("capacity", ent.Comp.ShieldCapacity)), ent, args.User);
    }

    private void OnShieldExamine(Entity<OrbitraRatvarMarauderComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("orbitra-ratvar-marauder-shield",
                ("charges", ent.Comp.ShieldCharges), ("capacity", ent.Comp.ShieldCapacity)));
    }

    private void OnShieldShutdown(Entity<OrbitraRatvarMarauderComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.RepairPending is { } pending)
            _doAfter.Cancel(pending);
    }

    private void OnShieldMobState(Entity<OrbitraRatvarMarauderComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive && ent.Comp.RepairPending is { } pending)
            _doAfter.Cancel(pending);
    }

    /// <summary>Consumes one body-owned charge and absorbs a single unspent projectile.</summary>
    public bool TryBlockProjectile(Entity<OrbitraRatvarMarauderComponent> ent, Entity<ProjectileComponent> projectile)
    {
        if (!CanBlockProjectile(ent, projectile))
            return false;
        ent.Comp.ShieldCharges--;
        // Помечаем до отложенного удаления, чтобы второй контакт в том же тике не потратил ещё заряд.
        projectile.Comp.ProjectileSpent = true;
        QueueDel(projectile);
        var broken = ent.Comp.ShieldCharges == 0;
        Spawn(broken ? ShieldBreakEffect : ShieldHitEffect, Transform(ent).Coordinates);
        _popup.PopupEntity(Loc.GetString(broken ? "orbitra-ratvar-marauder-shield-broken" :
            "orbitra-ratvar-marauder-shield-blocked"), ent, ent);
        return true;
    }

    private bool CanBlockProjectile(Entity<OrbitraRatvarMarauderComponent> ent, Entity<ProjectileComponent> projectile) =>
        !TerminatingOrDeleted(ent) && !TerminatingOrDeleted(projectile) && !projectile.Comp.ProjectileSpent &&
        ent.Comp.ShieldCharges > 0 && TryComp<MobStateComponent>(ent, out var mob) &&
        mob.CurrentState == MobState.Alive && !_container.IsEntityInContainer(ent);

    /// <summary>Starts an ordinary welding operation without requiring cult membership.</summary>
    public bool TryStartRepair(Entity<OrbitraRatvarMarauderComponent> ent, EntityUid user, EntityUid tool)
    {
        if (!CanRepair(ent, user, tool) || ent.Comp.RepairPending is { } running && _doAfter.IsRunning(running))
            return false;
        var handled = _tool.UseTool(tool, user, ent, ent.Comp.RepairDuration, Welding,
            new OrbitraRatvarMarauderRepairEvent(), out var pending, ent.Comp.RepairFuel);
        ent.Comp.RepairPending = pending;
        return handled;
    }

    private bool CanRepair(Entity<OrbitraRatvarMarauderComponent> ent, EntityUid user, EntityUid tool) =>
        !TerminatingOrDeleted(ent) && !TerminatingOrDeleted(user) && !TerminatingOrDeleted(tool) &&
        ent.Comp.RepairHealing > 0 && ent.Comp.RepairDuration > TimeSpan.Zero && ent.Comp.RepairFuel >= 0 &&
        TryComp<MobStateComponent>(ent, out var mob) && mob.CurrentState == MobState.Alive &&
        !_container.IsEntityInContainer(ent) && !_container.IsEntityInContainer(user) &&
        _hands.IsHolding(user, tool) && _blocker.CanInteract(user, ent) &&
        _interaction.InRangeUnobstructed(user, ent.Owner) &&
        (ent.Comp.ShieldCharges < ent.Comp.ShieldCapacity || _damageable.GetTotalDamage(ent.Owner) > 0);
}
