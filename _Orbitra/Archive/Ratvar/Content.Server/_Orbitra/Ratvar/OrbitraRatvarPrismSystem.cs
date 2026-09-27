using System.Numerics;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Powered cult healing with bounded toxin capture and a single destructive release.</summary>
public sealed partial class OrbitraRatvarPrismSystem : EntitySystem
{
    [Dependency] private OrbitraRatvarPowerSystem _power = default!;
    [Dependency] private OrbitraRatvarRuleSystem _rule = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SmokeSystem _smoke = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<DamageGroupPrototype> Brute = "Brute";
    private static readonly EntProtoId Smoke = "Smoke";
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<OrbitraRatvarPrismComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<OrbitraRatvarPrismComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<OrbitraRatvarPrismComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<OrbitraRatvarPrismComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<OrbitraRatvarPrismComponent, AnchorStateChangedEvent>(OnAnchor);
    }

    private void OnInit(Entity<OrbitraRatvarPrismComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextPulse = _timing.CurTime + ent.Comp.Interval;
        EnsureComp<ActiveOrbitraRatvarPrismComponent>(ent);
        UpdateAppearance(ent);
    }

    private void OnInteract(Entity<OrbitraRatvarPrismComponent> ent, ref InteractHandEvent args)
    {
        if (!args.Handled) args.Handled = TryToggle(ent, args.User);
    }

    private void OnExamine(Entity<OrbitraRatvarPrismComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("orbitra-ratvar-prism-status",
                ("enabled", HasComp<ActiveOrbitraRatvarPrismComponent>(ent)),
                ("volume", ent.Comp.Captured.Volume), ("capacity", ent.Comp.Capacity)));
    }

    private void OnDestroyed(Entity<OrbitraRatvarPrismComponent> ent, ref DestructionEventArgs args) => TryRelease(ent);

    private void OnAnchor(Entity<OrbitraRatvarPrismComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored) RemComp<ActiveOrbitraRatvarPrismComponent>(ent);
        UpdateAppearance(ent);
    }

    /// <summary>Only a nearby member of the owning active cult can toggle an anchored prism.</summary>
    public bool TryToggle(Entity<OrbitraRatvarPrismComponent> ent, EntityUid user)
    {
        if (!CanToggle(ent, user)) return false;
        if (HasComp<ActiveOrbitraRatvarPrismComponent>(ent)) RemComp<ActiveOrbitraRatvarPrismComponent>(ent);
        else EnsureComp<ActiveOrbitraRatvarPrismComponent>(ent);
        // Переключение не ускоряет лечение и не позволяет повторять импульс в одном тике.
        ent.Comp.NextPulse = _timing.CurTime + ent.Comp.Interval;
        UpdateAppearance(ent);
        return true;
    }

    private bool CanToggle(Entity<OrbitraRatvarPrismComponent> ent, EntityUid user) =>
        !TerminatingOrDeleted(ent) && !ent.Comp.Released && Transform(ent).Anchored &&
        !_container.IsEntityInContainer(ent) && _rule.TryGetCult(user, out var cult) &&
        TryComp<OrbitraRatvarStructureComponent>(ent, out var structure) && structure.Rule == cult.Owner &&
        _actionBlocker.CanInteract(user, ent) && _interaction.InRangeUnobstructed(user, ent.Owner);

    /// <summary>Runs at most one paid pulse; target membership and coverage are checked live.</summary>
    public bool TryPulse(Entity<OrbitraRatvarPrismComponent> ent)
    {
        if (!CanPulse(ent, out var powered, out var cult)) return false;
        ent.Comp.NextPulse = _timing.CurTime + ent.Comp.Interval;
        var healed = false;
        // Индекс сознаний конкретного культа, а не глобальный поиск мобов для каждой призмы.
        foreach (var mind in cult.Comp.Members)
        {
            if (!TryComp<MindComponent>(mind, out var mindComp) || mindComp.CurrentEntity is not { } body ||
                !CanHeal(ent, body, cult.Owner) || !NeedsHealing(ent, body)) continue;
            if (!_power.TryUsePower((ent, powered))) break;
            _damageable.TryChangeDamage(body, ent.Comp.Healing, true, origin: ent);
            _damageable.HealEvenly(body, -ent.Comp.BruteHealing, Brute, ent);
            if (TryComp<StaminaComponent>(body, out var stamina) && !stamina.Critical)
                _stamina.TakeStaminaDamage(body, -ent.Comp.StaminaHealing, stamina, visual: false);
            CaptureToxins(ent, body);
            healed = true;
        }
        return healed;
    }

    private bool CanPulse(Entity<OrbitraRatvarPrismComponent> ent,
        out OrbitraRatvarPoweredComponent powered, out Entity<OrbitraRatvarRuleComponent> cult)
    {
        cult = default;
        powered = default!;
        if (!TryComp<OrbitraRatvarPoweredComponent>(ent, out var power)) return false;
        powered = power;
        return !TerminatingOrDeleted(ent) && !ent.Comp.Released &&
            HasComp<ActiveOrbitraRatvarPrismComponent>(ent) && ent.Comp.Interval > TimeSpan.Zero &&
            float.IsFinite(ent.Comp.Radius) && ent.Comp.Radius > 0 && _timing.CurTime >= ent.Comp.NextPulse &&
            _power.CanUsePower((ent, powered), out cult, out _);
    }

    private bool CanHeal(Entity<OrbitraRatvarPrismComponent> ent, EntityUid body, EntityUid cult)
    {
        if (TerminatingOrDeleted(body) || EntityManager.IsQueuedForDeletion(body) || !TryComp<MobStateComponent>(body, out var mob) ||
            mob.CurrentState == MobState.Dead || _container.IsEntityInContainer(body) ||
            !_rule.TryGetCult(body, out var member) || member.Owner != cult) return false;
        var deviceTransform = Transform(ent);
        var bodyTransform = Transform(body);
        return bodyTransform.GridUid == deviceTransform.GridUid && bodyTransform.MapID == deviceTransform.MapID &&
            Vector2.DistanceSquared(_transform.GetWorldPosition(deviceTransform), _transform.GetWorldPosition(bodyTransform)) <=
            ent.Comp.Radius * ent.Comp.Radius;
    }

    private bool NeedsHealing(Entity<OrbitraRatvarPrismComponent> ent, EntityUid body)
    {
        if (TryComp<DamageableComponent>(body, out var damage))
        {
            var injuries = _damageable.GetPositiveDamage((body, damage));
            foreach (var (type, amount) in ent.Comp.Healing.DamageDict)
                if (amount < 0 && injuries.DamageDict.GetValueOrDefault(type) > 0) return true;
            if (_damageable.GetPositiveDamage((body, damage), Brute).GetTotal() > 0) return true;
        }
        return TryComp<StaminaComponent>(body, out var stamina) && !stamina.Critical &&
            _stamina.GetStaminaDamage(body, stamina) > 0;
    }

    private void CaptureToxins(Entity<OrbitraRatvarPrismComponent> ent, EntityUid body)
    {
        if (!TryComp<BloodstreamComponent>(body, out var blood) ||
            !_solutionContainer.TryGetSolution(body, blood.BloodSolutionName, out var solution, out var contents)) return;
        // Снимок нужен: штатное удаление реагента изменяет список раствора и вызывает события.
        foreach (var reagent in contents.Contents.ToArray())
        {
            if (!_prototype.TryIndex<ReagentPrototype>(reagent.Reagent.Prototype, out var prototype) ||
                prototype.Group != ent.Comp.ToxinGroup) continue;
            var amount = FixedPoint2.Min(reagent.Quantity, ent.Comp.Extraction,
                FixedPoint2.Max(0, ent.Comp.Capacity - ent.Comp.Captured.Volume));
            if (amount <= 0) continue;
            var removed = _solutionContainer.RemoveReagent(solution.Value, reagent.Reagent, amount);
            ent.Comp.Captured.AddReagent(reagent.Reagent, removed);
        }
    }

    /// <summary>Releases only captured material, once, on destructive damage (not round cleanup).</summary>
    public bool TryRelease(Entity<OrbitraRatvarPrismComponent> ent)
    {
        if (!CanRelease(ent)) return false;
        ent.Comp.Released = true;
        var contents = ent.Comp.Captured;
        ent.Comp.Captured = new Solution();
        var smoke = Spawn(Smoke, Transform(ent).Coordinates);
        // Штатное облако вмещает 600 ед., резервуар призмы 1000: не теряем остаток при передаче.
        if (_solutionContainer.TryGetSolution(smoke, SmokeComponent.SolutionName, out var solution, out var cloud))
            _solutionContainer.SetCapacity(solution.Value, FixedPoint2.Max(cloud.MaxVolume, contents.Volume));
        _smoke.StartSmoke(smoke, contents, ent.Comp.SmokeDuration, ent.Comp.SmokeSpread);
        return true;
    }

    private bool CanRelease(Entity<OrbitraRatvarPrismComponent> ent) =>
        !ent.Comp.Released && ent.Comp.Captured.Volume > 0 && ent.Comp.SmokeDuration > 0;

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate) return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        var query = EntityQueryEnumerator<ActiveOrbitraRatvarPrismComponent, OrbitraRatvarPrismComponent>();
        while (query.MoveNext(out var uid, out _, out var prism))
        {
            TryPulse((uid, prism));
            UpdateAppearance((uid, prism));
        }
    }

    private void UpdateAppearance(Entity<OrbitraRatvarPrismComponent> ent)
    {
        var state = !Transform(ent).Anchored ? "Unanchored" :
            HasComp<ActiveOrbitraRatvarPrismComponent>(ent) && TryComp<OrbitraRatvarPoweredComponent>(ent, out var powered) &&
            _power.CanUsePower((ent, powered), out _, out _) ? "Active" : "Inactive";
        _appearance.SetData(ent, OrbitraRatvarVisuals.Prism, state);
    }
}
