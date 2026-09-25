using Content.Server.Administration.Logs;
using Content.Server.Destructible;
using Content.Server.Mind;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Repairs the selected structure with server-side authority and cost revalidation.</summary>
public sealed partial class OrbitraRatvarFabricatorSystem : EntitySystem
{
    [Dependency] private readonly OrbitraRatvarRuleSystem _cult = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarFabricatorComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<OrbitraRatvarFabricatorComponent, OrbitraRatvarFabricatorEvent>(OnFinished);
        SubscribeLocalEvent<OrbitraRatvarFabricatorComponent, DoAfterAttemptEvent<OrbitraRatvarFabricatorEvent>>(OnAttempt);
        SubscribeLocalEvent<OrbitraRatvarFabricatorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OrbitraRatvarRepairTargetComponent, ComponentShutdown>(OnTargetShutdown);
    }

    private void OnInteract(Entity<OrbitraRatvarFabricatorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;
        args.Handled = true;
        if (!TryStartRepair(ent, args.User, target))
            _popup.PopupEntity(Loc.GetString("orbitra-ratvar-fabricator-denied", ("energy", ent.Comp.Energy)), ent, args.User);
    }

    private void OnAttempt(Entity<OrbitraRatvarFabricatorComponent> ent, ref DoAfterAttemptEvent<OrbitraRatvarFabricatorEvent> args)
    {
        var operation = args.DoAfter.Args;
        var context = (OrbitraRatvarFabricatorEvent) operation.Event;
        if (operation.Target is not { } target ||
            !CanRepair(ent, operation.User, target, out var rule, out var mind, out _) ||
            rule.Owner != GetEntity(context.Rule) || mind != GetEntity(context.Mind))
            args.Cancel();
    }

    private void OnFinished(Entity<OrbitraRatvarFabricatorComponent> ent, ref OrbitraRatvarFabricatorEvent args)
    {
        if (args.Target is { } targetUid && TryComp<OrbitraRatvarRepairTargetComponent>(targetUid, out var repairs))
            repairs.Pending.Remove(args.DoAfter.Id);
        if (args.Handled || ent.Comp.Pending != args.DoAfter.Id)
            return;
        args.Handled = true;
        ent.Comp.Pending = null;
        if (!args.Cancelled && args.Target is { } target)
            TryFinishRepair(ent, args.User, target, GetEntity(args.Rule), GetEntity(args.Mind));
    }

    private void OnShutdown(Entity<OrbitraRatvarFabricatorComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Pending is not { } pending)
            return;
        ent.Comp.Pending = null;
        if (_doAfter.IsRunning(pending))
            _doAfter.Cancel(pending);
    }

    private void OnTargetShutdown(Entity<OrbitraRatvarRepairTargetComponent> ent, ref ComponentShutdown args)
    {
        // Cancel синхронно вызывает OnFinished; удаляем запись до обратного вызова.
        while (ent.Comp.Pending.Count > 0)
        {
            var index = ent.Comp.Pending.Count - 1;
            var pending = ent.Comp.Pending[index];
            ent.Comp.Pending.RemoveAt(index);
            if (_doAfter.IsRunning(pending))
                _doAfter.Cancel(pending);
        }
    }

    /// <summary>Starts one interruptible repair; energy is only charged on successful completion.</summary>
    public bool TryStartRepair(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, EntityUid target)
    {
        if (tool.Comp.Pending != null || !CanRepair(tool, user, target, out var rule, out var mind, out _))
            return false;
        var operation = new DoAfterArgs(EntityManager, user, tool.Comp.Delay,
            new OrbitraRatvarFabricatorEvent { Rule = GetNetEntity(rule.Owner), Mind = GetNetEntity(mind) },
            tool, target: target, used: tool)
        {
            BreakOnMove = true, BreakOnDamage = true, BreakOnHandChange = true, NeedHand = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };
        if (!_doAfter.TryStartDoAfter(operation, out var id))
            return false;
        tool.Comp.Pending = id;
        if (id is { } pending && _doAfter.IsRunning(pending))
            EnsureComp<OrbitraRatvarRepairTargetComponent>(target).Pending.Add(pending);
        return true;
    }

    private bool TryFinishRepair(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user,
        EntityUid target, EntityUid originalRule, EntityUid originalMind)
    {
        if (!CanRepair(tool, user, target, out var rule, out var mind, out var amount) ||
            rule.Owner != originalRule || mind != originalMind)
            return false;
        // Списываем до событий урона; следующая операция увидит новый остаток.
        rule.Comp.Energy -= tool.Comp.Energy;
        _damage.HealEvenly(target, -amount, origin: user);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"Ratvar cult: {ToPrettyString(user)} repaired {ToPrettyString(target)} with {ToPrettyString(tool)}, spent {tool.Comp.Energy} from {ToPrettyString(rule)}.");
        _popup.PopupEntity(Loc.GetString("orbitra-ratvar-fabricator-repaired"), target, user);
        return true;
    }

    private bool CanRepair(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, EntityUid target,
        out Entity<OrbitraRatvarRuleComponent> rule, out EntityUid mind, out float amount)
    {
        rule = default;
        mind = default;
        amount = 0;
        if (TerminatingOrDeleted(tool) || TerminatingOrDeleted(user) || TerminatingOrDeleted(target) ||
            tool.Comp.Energy < 0 || tool.Comp.Delay <= TimeSpan.Zero || tool.Comp.RepairBonus < 0 ||
            !_cult.TryGetCult(user, out rule) || rule.Comp.Energy < tool.Comp.Energy ||
            !_mind.TryGetMind(user, out mind, out _) || !TryComp<MobStateComponent>(user, out var mob) ||
            mob.CurrentState != MobState.Alive || !_hands.IsHolding(user, tool) || !_blocker.CanInteract(user, target) ||
            _container.IsEntityInContainer(user) || _container.IsEntityInContainer(target) ||
            !Transform(target).Anchored || Transform(user).GridUid is not { } grid || Transform(target).GridUid != grid ||
            !_interaction.InRangeUnobstructed(user, target))
            return false;

        if (TryComp<OrbitraRatvarStructureComponent>(target, out var structure))
        {
            if (structure.Rule != rule.Owner)
                return false;
        }
        else if (Prototype(target) is not { } prototype || !tool.Comp.NeutralStructures.Contains(new EntProtoId(prototype.ID)))
            return false;

        if (!_destructible.TryGetDestroyedAt((target, null), out var threshold) || threshold == FixedPoint2.MaxValue)
            return false;
        var damage = _damage.GetTotalDamage(target).Float();
        var maximum = threshold.Value.Float();
        // Прочность Bee сопоставляется суммарному урону SS14; типы лечит штатная система.
        amount = Math.Min(damage, maximum - damage + tool.Comp.RepairBonus);
        return damage > 0 && damage < maximum && amount > 0;
    }
}
