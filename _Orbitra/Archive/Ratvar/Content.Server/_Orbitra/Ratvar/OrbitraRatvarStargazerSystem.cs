using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Server.Mind;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.ActionBlocker;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Authoritative weapon rituals and role-independent soul-tap effects.</summary>
public sealed partial class OrbitraRatvarStargazerSystem : EntitySystem
{
    [Dependency] private OrbitraRatvarRuleSystem _cult = default!;
    [Dependency] private OrbitraRatvarPowerSystem _power = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private FlammableSystem _flammable = default!;

    private static readonly ProtoId<DamageGroupPrototype> Brute = "Brute";
    private static readonly ProtoId<ItemSizePrototype> Tiny = "Tiny";
    private const SlotFlags WeaponStorageSlots = SlotFlags.BACK | SlotFlags.BELT | SlotFlags.SUITSTORAGE | SlotFlags.POCKET;

    public override void Initialize()
    {
        SubscribeLocalEvent<OrbitraRatvarStargazerComponent, InteractUsingEvent>(OnInteract);
        SubscribeLocalEvent<OrbitraRatvarStargazerComponent, OrbitraRatvarEnchantEvent>(OnFinished);
        SubscribeLocalEvent<OrbitraRatvarStargazerComponent, DoAfterAttemptEvent<OrbitraRatvarEnchantEvent>>(OnAttempt);
        SubscribeLocalEvent<OrbitraRatvarStargazerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OrbitraRatvarStargazerComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<OrbitraRatvarStargazerComponent, AnchorStateChangedEvent>(OnAnchor);
        SubscribeLocalEvent<OrbitraRatvarStargazerComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<OrbitraRatvarEnchantmentComponent, ExaminedEvent>(OnWeaponExamine);
        SubscribeLocalEvent<OrbitraRatvarEnchantmentComponent, MeleeHitEvent>(OnHit);
    }

    private void OnInit(Entity<OrbitraRatvarStargazerComponent> ent, ref MapInitEvent args) => UpdateAppearance(ent);

    private void OnAnchor(Entity<OrbitraRatvarStargazerComponent> ent, ref AnchorStateChangedEvent args) => UpdateAppearance(ent);

    private void UpdateAppearance(Entity<OrbitraRatvarStargazerComponent> ent)
    {
        var state = !Transform(ent).Anchored ? "Unanchored" : ent.Comp.Pending != null ? "Active" : "Idle";
        _appearance.SetData(ent, OrbitraRatvarVisuals.Enchanting, state);
    }

    private void OnInteract(Entity<OrbitraRatvarStargazerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;
        if (!TryStart(ent, args.User, args.Used))
            _popup.PopupEntity(Loc.GetString("orbitra-ratvar-enchant-denied"), ent, args.User);
    }

    private void OnExamine(Entity<OrbitraRatvarStargazerComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("orbitra-ratvar-enchant-status",
                ("seconds", Math.Max(0, (int) Math.Ceiling((ent.Comp.NextEnchant - _timing.CurTime).TotalSeconds))),
                ("busy", ent.Comp.Pending != null)));
    }

    private void OnWeaponExamine(Entity<OrbitraRatvarEnchantmentComponent> ent, ref ExaminedEvent args) =>
        args.PushMarkup(_cult.TryGetCult(args.Examiner, out _)
            ? Loc.GetString("orbitra-ratvar-enchant-description", ("kind", ent.Comp.Kind.ToString()), ("level", ent.Comp.Level))
            : Loc.GetString("orbitra-ratvar-enchant-glow"));

    private void OnAttempt(Entity<OrbitraRatvarStargazerComponent> ent, ref DoAfterAttemptEvent<OrbitraRatvarEnchantEvent> args)
    {
        var operation = args.DoAfter.Args;
        var context = (OrbitraRatvarEnchantEvent) operation.Event;
        if (operation.Used is not { } weapon || !CanEnchant(ent, operation.User, weapon, out var rule, out var mind) ||
            rule != GetEntity(context.Rule) || mind != GetEntity(context.Mind)) args.Cancel();
    }

    private void OnShutdown(Entity<OrbitraRatvarStargazerComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Pending is not { } pending) return;
        ent.Comp.Pending = null;
        if (_doAfter.IsRunning(pending)) _doAfter.Cancel(pending);
    }

    private void OnFinished(Entity<OrbitraRatvarStargazerComponent> ent, ref OrbitraRatvarEnchantEvent args)
    {
        if (args.Handled || ent.Comp.Pending != args.DoAfter.Id) return;
        args.Handled = true;
        ent.Comp.Pending = null;
        UpdateAppearance(ent);
        if (args.Cancelled || args.Used is not { } weapon ||
            !CanEnchant(ent, args.User, weapon, out var rule, out var mind) ||
            rule != GetEntity(args.Rule) || mind != GetEntity(args.Mind)) return;
        ent.Comp.NextEnchant = _timing.CurTime + ent.Comp.Cooldown;
        var kind = _random.Pick(ent.Comp.Enchantments.Keys.ToArray());
        var level = _random.Next(1, ent.Comp.Enchantments[kind] + 1);
        var blessing = AddComp<OrbitraRatvarEnchantmentComponent>(weapon);
        blessing.Kind = kind;
        blessing.Level = level;
        var melee = Comp<MeleeWeaponComponent>(weapon);
        if (kind == OrbitraRatvarEnchantment.Sharpness)
        {
            var total = melee.Damage.GetTotal().Float();
            foreach (var (type, amount) in melee.Damage.DamageDict)
                if (amount > 0) blessing.Bonus.DamageDict[type] = amount / total * (ent.Comp.SharpnessPerLevel * level);
        }
        else if (kind is OrbitraRatvarEnchantment.Tiny or OrbitraRatvarEnchantment.Burn)
        {
            _item.SetShape(weapon, null);
            _item.SetSize(weapon, Tiny);
            if (kind == OrbitraRatvarEnchantment.Burn)
                blessing.FireStacks = ent.Comp.FireStacksPerLevel * level;
        }
        else blessing.HealingFraction = ent.Comp.SoulHealingPerLevel;
        Dirty(weapon, blessing);
        Spawn(ent.Comp.Effect, Transform(ent).Coordinates);
        _audio.PlayPvs(ent.Comp.Sound, ent);
        _popup.PopupEntity(Loc.GetString("orbitra-ratvar-enchant-success"), ent, args.User);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"Ratvar enchantment: {ToPrettyString(args.User)} enchanted {ToPrettyString(weapon)} with {kind} {level} at {ToPrettyString(ent)}.");
    }

    /// <summary>Reserves one machine; completion repeats body, mind, item and power checks.</summary>
    public bool TryStart(Entity<OrbitraRatvarStargazerComponent> ent, EntityUid user, EntityUid weapon)
    {
        if (ent.Comp.Pending != null || !CanEnchant(ent, user, weapon, out var rule, out var mind)) return false;
        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.Delay,
                new OrbitraRatvarEnchantEvent { Rule = GetNetEntity(rule), Mind = GetNetEntity(mind) }, ent,
                target: ent, used: weapon)
            {
                BreakOnMove = true, BreakOnDamage = true, BreakOnHandChange = true, NeedHand = true,
                AttemptFrequency = AttemptFrequency.EveryTick,
            }, out var id)) return false;
        ent.Comp.Pending = id;
        UpdateAppearance(ent);
        return true;
    }

    private bool CanEnchant(Entity<OrbitraRatvarStargazerComponent> ent, EntityUid user, EntityUid weapon,
        out EntityUid rule, out EntityUid mind)
    {
        rule = default;
        mind = default;
        if (TerminatingOrDeleted(ent) || EntityManager.IsQueuedForDeletion(ent) ||
            TerminatingOrDeleted(weapon) || EntityManager.IsQueuedForDeletion(weapon) ||
            ent.Comp.NextEnchant > _timing.CurTime || ent.Comp.Delay <= TimeSpan.Zero || ent.Comp.Cooldown < TimeSpan.Zero ||
            ent.Comp.Enchantments.Count == 0 || !float.IsFinite(ent.Comp.FireStacksPerLevel) || ent.Comp.FireStacksPerLevel < 0 ||
            !_cult.TryGetCult(user, out var cult) ||
            !_mind.TryGetMind(user, out mind, out var mindComp) || mindComp.CurrentEntity != user ||
            !_blocker.CanInteract(user, ent) || !_hands.IsHolding(user, weapon) ||
            !_interaction.InRangeUnobstructed(user, ent.Owner) || !HasComp<ItemComponent>(weapon) ||
            (TryComp<ClothingComponent>(weapon, out var clothing) && (clothing.Slots & ~WeaponStorageSlots) != 0) ||
            HasComp<OrbitraRatvarEnchantmentComponent>(weapon) ||
            !TryComp<MeleeWeaponComponent>(weapon, out var melee) || melee.Damage.GetTotal() <= 0 ||
            !TryComp<OrbitraRatvarPoweredComponent>(ent, out var powered) ||
            !_power.CanUsePower((ent, powered), out var owner, out _) || owner.Owner != cult.Owner) return false;
        foreach (var (kind, max) in ent.Comp.Enchantments)
            if (!Enum.IsDefined(kind) || max < 1 || max > 5) return false;
        rule = cult.Owner;
        return true;
    }

    private void OnHit(Entity<OrbitraRatvarEnchantmentComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.Handled) return;
        if (ent.Comp.Kind == OrbitraRatvarEnchantment.Burn)
        {
            IgniteHitTargets(ent, args);
            return;
        }
        if (ent.Comp.Kind != OrbitraRatvarEnchantment.SoulTap ||
            !TryComp<MeleeWeaponComponent>(ent, out var melee)) return;
        foreach (var target in args.HitEntities)
        {
            if (target == args.User || TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target) ||
                !TryComp<MobStateComponent>(target, out var mob) || mob.CurrentState != MobState.Alive) continue;
            var amount = MathF.Ceiling(ent.Comp.Level * melee.Damage.GetTotal().Float() * ent.Comp.HealingFraction);
            _damage.HealEvenly(args.User, -amount, Brute, ent);
            var heat = new DamageSpecifier();
            heat.DamageDict.Add("Heat", -amount);
            _damage.TryChangeDamage(args.User, heat, true, origin: ent);
            // Один взмах не умножает лечение на число целей широкой атаки SS14.
            break;
        }
    }

    private void IgniteHitTargets(Entity<OrbitraRatvarEnchantmentComponent> ent, MeleeHitEvent args)
    {
        if (!float.IsFinite(ent.Comp.FireStacks) || ent.Comp.FireStacks <= 0) return;
        for (var i = 0; i < args.HitEntities.Count; i++)
        {
            var target = args.HitEntities[i];
            if (target == args.User || TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target) ||
                !HasComp<MobStateComponent>(target) || !TryComp<FlammableComponent>(target, out var flammable)) continue;
            // Один взмах добавляет стаки каждой цели только один раз, без отдельной коллекции.
            var duplicate = false;
            for (var previous = 0; previous < i; previous++)
                if (args.HitEntities[previous] == target) { duplicate = true; break; }
            if (duplicate) continue;
            _flammable.AdjustFireStacks(target, ent.Comp.FireStacks, flammable);
            _flammable.Ignite(target, ent, flammable, args.User);
        }
    }
}
