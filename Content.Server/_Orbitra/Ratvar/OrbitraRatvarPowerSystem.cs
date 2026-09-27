using System.Numerics;
using Content.Server.GameTicking;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Examine;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Localization;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Validates local coverage and atomically consumes the owning cult's shared energy.</summary>
public sealed partial class OrbitraRatvarPowerSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextVisualUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarTransmissionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OrbitraRatvarPoweredComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<OrbitraRatvarPoweredComponent, AttemptShootEvent>(OnShootAttempt);
        SubscribeLocalEvent<OrbitraRatvarPoweredComponent, SelfBeforeGunShotEvent>(OnBeforeShot);
    }

    private void OnShutdown(Entity<OrbitraRatvarTransmissionComponent> ent, ref ComponentShutdown args) =>
        RemoveIndex(ent);

    private void OnExamine(Entity<OrbitraRatvarPoweredComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange) return;
        CanUsePower(ent, out _, out var reason);
        args.PushMarkup(Loc.GetString(reason));
        args.PushMarkup(Loc.GetString("orbitra-ratvar-power-cost",
            ("cost", ent.Comp.EnergyPerUse), ("minimum", ent.Comp.MinimumEnergy)));
    }

    private void OnShootAttempt(Entity<OrbitraRatvarPoweredComponent> ent, ref AttemptShootEvent args)
    {
        if (!CanUsePower(ent, out _, out _)) args.Cancelled = true;
    }

    private void OnBeforeShot(Entity<OrbitraRatvarPoweredComponent> ent, ref SelfBeforeGunShotEvent args)
    {
        // Турель стреляет собственным оружием. Проверка перед выпуском снарядов закрывает
        // также вызов GunSystem.Shoot с турелью в качестве стрелка в обход AttemptShootEvent.
        if (args.Cancelled || args.Gun.Owner != ent.Owner) return;
        if (!TryUsePower(ent, args.Ammo.Count)) args.Cancel();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextVisualUpdate) return;
        _nextVisualUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        var query = EntityQueryEnumerator<OrbitraRatvarPoweredComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out var powered, out var appearance))
            _appearance.SetData(uid, OrbitraRatvarVisuals.Powered,
                CanUsePower((uid, powered), out _, out _), appearance);
    }

    /// <summary>Registers a newly created sigil or transfers its coverage to another cult.</summary>
    public void BindTransmission(Entity<OrbitraRatvarTransmissionComponent> ent, EntityUid? rule)
    {
        RemoveIndex(ent);
        if (!TryComp<OrbitraRatvarStructureComponent>(ent, out var structure)) return;
        structure.Rule = rule;
        if (rule == null || !TryComp<OrbitraRatvarRuleComponent>(rule, out var cult)) return;
        cult.TransmissionSigils.Add(ent);
        ent.Comp.IndexedRule = rule;
    }

    public bool TryUsePower(Entity<OrbitraRatvarPoweredComponent> ent, int uses = 1)
    {
        if (!CanUsePower(ent, out var rule, out _, uses)) return false;
        rule.Comp.Energy -= ent.Comp.EnergyPerUse * uses;
        return true;
    }

    /// <summary>Checks current authority without side effects; cached UI state never authorizes expenditure.</summary>
    public bool CanUsePower(Entity<OrbitraRatvarPoweredComponent> ent,
        out Entity<OrbitraRatvarRuleComponent> rule, out LocId reason, int uses = 1)
    {
        rule = default;
        reason = "orbitra-ratvar-power-unbound";
        if (TerminatingOrDeleted(ent) || EntityManager.IsQueuedForDeletion(ent) ||
            !TryComp<OrbitraRatvarStructureComponent>(ent, out var structure) ||
            structure.Rule is not { } owner || !TryComp<OrbitraRatvarRuleComponent>(owner, out var cult) ||
            TerminatingOrDeleted(owner) || EntityManager.IsQueuedForDeletion(owner) ||
            cult.Won || cult.Lost || !_gameTicker.IsGameRuleActive(owner)) return false;
        rule = (owner, cult);
        reason = "orbitra-ratvar-power-unanchored";
        var xform = Transform(ent);
        if (!xform.Anchored || xform.GridUid is not { } grid || _container.IsEntityInContainer(ent)) return false;
        reason = "orbitra-ratvar-power-uncovered";
        if (!HasCoverage(owner, cult, grid, _transform.GetWorldPosition(xform))) return false;
        reason = "orbitra-ratvar-power-empty";
        var cost = (long) ent.Comp.EnergyPerUse * uses;
        if (uses <= 0 || ent.Comp.EnergyPerUse < 0 || ent.Comp.MinimumEnergy < 0 ||
            cost > int.MaxValue || cult.Energy < Math.Max(cost, ent.Comp.MinimumEnergy)) return false;
        reason = "orbitra-ratvar-power-ready";
        return true;
    }

    private bool HasCoverage(EntityUid owner, OrbitraRatvarRuleComponent cult, EntityUid grid, Vector2 position)
    {
        // Только печати этого культа, не глобальный обход механизмов на каждый выстрел.
        foreach (var uid in cult.TransmissionSigils)
        {
            if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid) ||
                !TryComp<OrbitraRatvarTransmissionComponent>(uid, out var sigil) ||
                !TryComp<OrbitraRatvarStructureComponent>(uid, out var structure) || structure.Rule != owner ||
                !float.IsFinite(sigil.Radius) || sigil.Radius < 0) continue;
            var xform = Transform(uid);
            if (!xform.Anchored || xform.GridUid != grid || _container.IsEntityInContainer(uid)) continue;
            if (Vector2.DistanceSquared(position, _transform.GetWorldPosition(xform)) <= sigil.Radius * sigil.Radius)
                return true;
        }
        return false;
    }

    private void RemoveIndex(Entity<OrbitraRatvarTransmissionComponent> ent)
    {
        if (ent.Comp.IndexedRule is { } owner && TryComp<OrbitraRatvarRuleComponent>(owner, out var cult))
            cult.TransmissionSigils.Remove(ent);
        ent.Comp.IndexedRule = null;
    }
}
