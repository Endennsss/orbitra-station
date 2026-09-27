using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarRuleSystem
{
    // Подготовка, проверка цели и расход разделены; сеть не передаёт готовое разрешение на эффект.
    [Dependency] private SharedCuffableSystem _cuffable = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    private static readonly EntProtoId VanguardStatus = "OrbitraRatvarVanguardStatus";
    private static readonly EntProtoId MutedStatus = "StatusEffectMuted";
    private static readonly EntProtoId KnockdownStatus = "StatusEffectKnockdown";
    private static readonly EntProtoId Manacles = "OrbitraRatvarManacles";
    private static readonly ProtoId<DamageTypePrototype> BacklashDamage = "Poison";
    private static readonly string[] CompromiseDamage = ["Blunt", "Slash", "Piercing", "Heat", "Asphyxiation", "Cellular"];

    private void InitializeEmpowerments()
    {
        SubscribeLocalEvent<OrbitraRatvarTabletComponent, OrbitraRatvarTargetSpellEvent>(OnTargetSpellFinished);
        SubscribeLocalEvent<OrbitraRatvarTabletComponent, DoAfterAttemptEvent<OrbitraRatvarTargetSpellEvent>>(OnTargetSpellAttempt);
        SubscribeLocalEvent<ActiveOrbitraRatvarVanguardComponent, BeforeStatusEffectAddedEvent>(OnVanguardStatus);
        SubscribeLocalEvent<ActiveOrbitraRatvarVanguardComponent, KnockDownAttemptEvent>(OnVanguardKnockdown);
        SubscribeLocalEvent<ActiveOrbitraRatvarVanguardComponent, BeforeStaminaDamageEvent>(OnVanguardStamina);
    }

    private void OnVanguardStatus(Entity<ActiveOrbitraRatvarVanguardComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        if (HasActiveVanguard(ent) && (args.Effect == SharedStunSystem.StunId || args.Effect == KnockdownStatus))
            args.Cancelled = true;
    }

    private void OnVanguardKnockdown(Entity<ActiveOrbitraRatvarVanguardComponent> ent, ref KnockDownAttemptEvent args)
    {
        if (HasActiveVanguard(ent)) args.Cancelled = true;
    }

    private void OnVanguardStamina(Entity<ActiveOrbitraRatvarVanguardComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        if (args.Value > 0 && HasActiveVanguard(ent)) args.Cancelled = true;
    }

    private void OnTargetSpellAttempt(Entity<OrbitraRatvarTabletComponent> ent, ref DoAfterAttemptEvent<OrbitraRatvarTargetSpellEvent> args)
    {
        if (args.DoAfter.Args.Target is not { } target || !CanTargetEmpowerment(ent, args.DoAfter.Args.User, target, out _, out _))
            args.Cancel();
    }

    private void OnTargetSpellFinished(Entity<OrbitraRatvarTabletComponent> ent, ref OrbitraRatvarTargetSpellEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;
        ent.Comp.Busy = false;
        if (!args.Cancelled && args.Target is { } target)
            TryApplyEmpowerment(ent, args.User, target);
        RemComp<ActiveOrbitraRatvarEmpowermentComponent>(ent);
    }

    private bool TryPrepareEmpowerment(Entity<OrbitraRatvarTabletComponent> tablet, EntityUid user,
        Entity<OrbitraRatvarRuleComponent> rule, OrbitraRatvarScripturePrototype scripture)
    {
        if (!_mind.TryGetMind(user, out var mind, out var mindComp) || mindComp.CurrentEntity != user) return false;
        if (scripture.Empowerment == OrbitraRatvarEmpowerment.Vanguard)
        {
            if (!_statusEffects.TryAddStatusEffectDuration(user, VanguardStatus, scripture.EffectDuration)) return false;
            var defence = EnsureComp<ActiveOrbitraRatvarVanguardComponent>(user);
            defence.Mind = mind;
            defence.Rule = rule.Owner;
            defence.Expires = Timing.CurTime + scripture.EffectDuration;
            rule.Comp.Energy -= scripture.Energy;
            _popup.PopupEntity(Loc.GetString("orbitra-ratvar-vanguard-start"), user, user);
            _adminLog.Add(LogType.Action, LogImpact.Medium, $"Ratvar: {ToPrettyString(user)} invoked {scripture.ID}, spent {scripture.Energy}.");
            return true;
        }

        var empowered = EnsureComp<ActiveOrbitraRatvarEmpowermentComponent>(tablet);
        empowered.User = user;
        empowered.Mind = mind;
        empowered.Rule = rule.Owner;
        empowered.Scripture = scripture.ID;
        empowered.Expires = Timing.CurTime + scripture.TargetWindow;
        _ui.CloseUi(tablet.Owner, OrbitraRatvarUiKey.Key, user);
        _popup.PopupEntity(Loc.GetString("orbitra-ratvar-spell-ready", ("spell", Loc.GetString(scripture.Name))), user, user);
        return true;
    }

    /// <summary>Starts a targeted operation without trusting previously prepared UI state.</summary>
    public bool TryTargetEmpowerment(Entity<OrbitraRatvarTabletComponent> tablet, EntityUid user, EntityUid target)
    {
        if (tablet.Comp.Busy) return false;
        if (!CanTargetEmpowerment(tablet, user, target, out _, out var scripture))
        {
            _popup.PopupEntity(Loc.GetString("orbitra-ratvar-spell-invalid"), user, user);
            return false;
        }
        if (scripture.TargetDelay <= TimeSpan.Zero) return TryApplyEmpowerment(tablet, user, target);
        var armed = Comp<ActiveOrbitraRatvarEmpowermentComponent>(tablet);
        armed.Target = target;
        tablet.Comp.Busy = true;
        var args = new DoAfterArgs(EntityManager, user, scripture.TargetDelay, new OrbitraRatvarTargetSpellEvent(), tablet, target, tablet)
        {
            BreakOnMove = true, BreakOnDamage = true, BreakOnHandChange = true, NeedHand = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };
        if (_doAfter.TryStartDoAfter(args)) return true;
        tablet.Comp.Busy = false;
        RemComp<ActiveOrbitraRatvarEmpowermentComponent>(tablet);
        return false;
    }

    /// <summary>Checks range, sight, original caster, cult and current target eligibility without mutation.</summary>
    public bool CanTargetEmpowerment(Entity<OrbitraRatvarTabletComponent> tablet, EntityUid user, EntityUid target,
        out Entity<OrbitraRatvarRuleComponent> rule, out OrbitraRatvarScripturePrototype scripture)
    {
        rule = default;
        scripture = default!;
        if (!TryComp<ActiveOrbitraRatvarEmpowermentComponent>(tablet, out var armed) || armed.User != user ||
            !ValidEmpowerment(tablet, armed) || !TryGetCult(user, out rule) ||
            !_prototypes.TryIndex(armed.Scripture, out var found) || found.Energy < 0 ||
            rule.Comp.Energy < found.Energy || GetTier(rule.Comp) < found.Tier ||
            target == user || TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target) || !Living(target) ||
            _containers.IsEntityInContainer(target) || !Near(user, target, found.TargetRange) ||
            !_interaction.InRangeUnobstructed(user, target, range: found.TargetRange) ||
            armed.Target is { } pending && pending != target) return false;
        scripture = found;
        var cultist = TryGetCult(target, out var targetRule);
        switch (scripture.Empowerment)
        {
            case OrbitraRatvarEmpowerment.Compromise:
                return cultist && targetRule.Owner == rule.Owner && !HasComp<GodmodeComponent>(user) &&
                    HasComp<DamageableComponent>(user) && _damage.CanBeDamagedBy((user, null), BacklashDamage) &&
                    TryComp<DamageableComponent>(target, out var damage) && GetCompromiseHealing((target, damage), scripture).GetTotal() < 0;
            case OrbitraRatvarEmpowerment.Manacles:
                return !cultist && HasComp<HumanoidProfileComponent>(target) &&
                    TryComp<CuffableComponent>(target, out var cuffs) && cuffs.CuffedHandCount == 0 &&
                    TryComp<HandsComponent>(target, out var hands) && hands.Count >= 2;
            case OrbitraRatvarEmpowerment.Kindle:
                return !cultist && !HasActiveVanguard(target) &&
                    !(_mind.TryGetMind(target, out var targetMind, out _) && _jobs.MindTryGetJobId(targetMind, out var job) && job == "Chaplain");
            default: return false;
        }
    }

    private bool TryApplyEmpowerment(Entity<OrbitraRatvarTabletComponent> tablet, EntityUid user, EntityUid target)
    {
        if (!CanTargetEmpowerment(tablet, user, target, out var rule, out var scripture)) return false;
        // Резерв снимает повторный вход через события урона/наручников; при отказе возвращаем оплату.
        RemComp<ActiveOrbitraRatvarEmpowermentComponent>(tablet);
        rule.Comp.Energy -= scripture.Energy;
        var applied = false;
        switch (scripture.Empowerment)
        {
            case OrbitraRatvarEmpowerment.Manacles:
                var cuffs = Spawn(Manacles, Transform(target).Coordinates);
                applied = _cuffable.TryAddNewCuffs(target, user, cuffs);
                if (!applied) QueueDel(cuffs);
                break;
            case OrbitraRatvarEmpowerment.Kindle:
                if (!TryComp<MindShieldStatusComponent>(target, out var shield) || !shield.IsMindshielded)
                    applied = _stun.TryUpdateParalyzeDuration(target, scripture.EffectDuration, true);
                applied |= _statusEffects.TryUpdateStatusEffectDuration(target, MutedStatus, scripture.MuteDuration);
                break;
            case OrbitraRatvarEmpowerment.Compromise:
                var damage = Comp<DamageableComponent>(target);
                if (_damage.TryChangeDamage((target, damage), GetCompromiseHealing((target, damage), scripture), out var healed,
                        ignoreResistances: true, interruptsDoAfters: false, origin: user, ignoreGlobalModifiers: true))
                {
                    var backlash = new DamageSpecifier();
                    backlash.DamageDict["Poison"] = Math.Min(scripture.BacklashCap, -healed.GetTotal().Float() * scripture.BacklashFraction);
                    _damage.TryChangeDamage((user, null), backlash, ignoreResistances: true, origin: user, ignoreGlobalModifiers: true);
                    applied = true;
                }
                break;
        }
        if (!applied) rule.Comp.Energy += scripture.Energy;
        else
        {
            _popup.PopupEntity(Loc.GetString("orbitra-ratvar-spell-applied", ("spell", Loc.GetString(scripture.Name))), target, user);
            _adminLog.Add(LogType.Action, LogImpact.Medium, $"Ratvar: {ToPrettyString(user)} applied {scripture.ID} to {ToPrettyString(target)}, spent {scripture.Energy}.");
        }
        return applied;
    }

    private DamageSpecifier GetCompromiseHealing(Entity<DamageableComponent> target, OrbitraRatvarScripturePrototype scripture)
    {
        var healing = new DamageSpecifier();
        var damage = _damage.GetAllDamage(target.AsNullable());
        foreach (var type in CompromiseDamage)
            if (damage.DamageDict.TryGetValue(type, out var amount) && amount > 0)
                healing.DamageDict[type] = -amount * scripture.HealingFraction;
        return healing;
    }

    private bool ValidEmpowerment(EntityUid tablet, ActiveOrbitraRatvarEmpowermentComponent armed) =>
        !TerminatingOrDeleted(tablet) && !EntityManager.IsQueuedForDeletion(tablet) &&
        (armed.Target != null || Timing.CurTime < armed.Expires) &&
        TryGetCult(armed.User, out var rule) && rule.Owner == armed.Rule && Living(armed.User) &&
        CanReciteInBody(armed.User) && !HasActiveVanguard(armed.User) && _hands.IsHolding(armed.User, tablet) &&
        _blocker.CanInteract(armed.User, tablet) && !_containers.IsEntityInContainer(armed.User) &&
        _mind.TryGetMind(armed.User, out var mind, out var data) && mind == armed.Mind && data.CurrentEntity == armed.User;

    private bool HasActiveVanguard(EntityUid user) =>
        TryComp<ActiveOrbitraRatvarVanguardComponent>(user, out var defence) && Timing.CurTime < defence.Expires &&
        Living(user) && TryGetCult(user, out var cult) && cult.Owner == defence.Rule &&
        _mind.TryGetMind(user, out var mind, out var data) && mind == defence.Mind && data.CurrentEntity == user;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateSubmissionSigils();
        var empowered = EntityQueryEnumerator<ActiveOrbitraRatvarEmpowermentComponent>();
        while (empowered.MoveNext(out var uid, out var armed))
            if (!ValidEmpowerment(uid, armed)) RemCompDeferred<ActiveOrbitraRatvarEmpowermentComponent>(uid);
        var defenders = EntityQueryEnumerator<ActiveOrbitraRatvarVanguardComponent>();
        while (defenders.MoveNext(out var uid, out _))
        {
            if (HasActiveVanguard(uid)) continue;
            _statusEffects.TryRemoveStatusEffect(uid, VanguardStatus);
            _popup.PopupEntity(Loc.GetString("orbitra-ratvar-vanguard-end"), uid, uid);
            RemCompDeferred<ActiveOrbitraRatvarVanguardComponent>(uid);
        }
    }
}
