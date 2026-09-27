using Content.Server.Construction.Components;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarFabricatorSystem
{
    private void InitializeWindows()
    {
        SubscribeLocalEvent<OrbitraRatvarFabricatorComponent, OrbitraRatvarWindowEvent>(OnWindowFinished);
        SubscribeLocalEvent<OrbitraRatvarFabricatorComponent, DoAfterAttemptEvent<OrbitraRatvarWindowEvent>>(OnWindowAttempt);
    }

    private void OnWindowAttempt(Entity<OrbitraRatvarFabricatorComponent> ent, ref DoAfterAttemptEvent<OrbitraRatvarWindowEvent> args)
    {
        if (args.DoAfter.Args.Target is not { } target ||
            !CanConvertWindow(ent, args.DoAfter.Args.User, target, (OrbitraRatvarWindowEvent) args.DoAfter.Args.Event, out _))
            args.Cancel();
    }

    private void OnWindowFinished(Entity<OrbitraRatvarFabricatorComponent> ent, ref OrbitraRatvarWindowEvent args)
    {
        if (args.Target is { } target && TryComp<OrbitraRatvarRepairTargetComponent>(target, out var operations))
            operations.Pending.Remove(args.DoAfter.Id);
        if (args.Handled || ent.Comp.Pending != args.DoAfter.Id)
            return;
        args.Handled = true;
        ent.Comp.Pending = null;
        if (!args.Cancelled && args.Target is { } window)
            TryFinishWindow(ent, args.User, window, args);
    }

    /// <summary>Starts conversion of an explicitly supported full-tile window without consuming resources.</summary>
    public bool TryStartWindow(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, EntityUid target)
    {
        if (tool.Comp.Pending != null || !_cult.TryGetCult(user, out var rule) ||
            !_mind.TryGetMind(user, out var mind, out _) || Transform(user).GridUid is not { } grid ||
            Transform(grid).MapUid is not { } map)
            return false;
        var context = new OrbitraRatvarWindowEvent
        {
            Mind = GetNetEntity(mind), Rule = GetNetEntity(rule.Owner),
            Grid = GetNetEntity(grid), Map = GetNetEntity(map),
        };
        if (!CanConvertWindow(tool, user, target, context, out _))
            return false;
        var operation = new DoAfterArgs(EntityManager, user, tool.Comp.WindowDelay, context, tool, target: target, used: tool)
        {
            BreakOnMove = true, BreakOnDamage = true, BreakOnHandChange = true, NeedHand = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };
        if (!_doAfter.TryStartDoAfter(operation, out var id))
            return false;
        tool.Comp.Pending = id;
        // Отмена при удалении цели использует тот же список, что ремонт и стены.
        if (id is { } pending && _doAfter.IsRunning(pending))
            EnsureComp<OrbitraRatvarRepairTargetComponent>(target).Pending.Add(pending);
        return true;
    }

    private bool CanConvertWindow(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, EntityUid target,
        OrbitraRatvarWindowEvent context, out Entity<OrbitraRatvarRuleComponent> rule)
    {
        rule = default;
        if (TerminatingOrDeleted(tool) || TerminatingOrDeleted(user) || TerminatingOrDeleted(target) ||
            tool.Comp.WindowEnergy < 0 || tool.Comp.WindowDelay <= TimeSpan.Zero ||
            !_cult.TryGetCult(user, out rule) || rule.Owner != GetEntity(context.Rule) ||
            rule.Comp.Energy < tool.Comp.WindowEnergy || !_mind.TryGetMind(user, out var mind, out _) ||
            mind != GetEntity(context.Mind) || !TryComp<MobStateComponent>(user, out var mob) || mob.CurrentState != MobState.Alive ||
            !_hands.IsHolding(user, tool) || !_blocker.CanInteract(user, target) ||
            _container.IsEntityInContainer(user) || _container.IsEntityInContainer(target) ||
            Prototype(target) is not { } prototype || !tool.Comp.Windows.TryGetValue(new EntProtoId(prototype.ID), out var sourceNode))
            return false;

        var transform = Transform(target);
        if (!transform.Anchored || transform.ChildCount != 0 || HasComp<ContainerManagerComponent>(target) ||
            transform.GridUid != GetEntity(context.Grid) || Transform(user).GridUid != transform.GridUid ||
            transform.MapUid != GetEntity(context.Map) || _damage.GetTotalDamage(target) != 0 ||
            !TryComp<ConstructionComponent>(target, out var construction) ||
            construction.Graph.Id != "Window" || construction.Node != sourceNode ||
            construction.TargetNode != null || construction.InteractionQueue.Count != 0)
            return false;
        return _interaction.InRangeUnobstructed(user, target);
    }

    private bool TryFinishWindow(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, EntityUid target,
        OrbitraRatvarWindowEvent context)
    {
        if (!CanConvertWindow(tool, user, target, context, out var rule))
            return false;
        rule.Comp.Energy -= tool.Comp.WindowEnergy;
        // Меняем граф через штатную систему: результат сохраняет собственную разборку.
        if (!_construction.ChangeGraph(target, user, "OrbitraRatvarWindow", "window", performActions: false))
        {
            rule.Comp.Energy += tool.Comp.WindowEnergy;
            return false;
        }
        _adminLog.Add(LogType.Construction, LogImpact.Medium,
            $"Ratvar cult: {ToPrettyString(user)} converted {ToPrettyString(target)} to OrbitraRatvarWindow, spent {tool.Comp.WindowEnergy} from {ToPrettyString(rule)}.");
        _popup.PopupEntity(Loc.GetString("orbitra-ratvar-fabricator-window-done"), tool, user);
        return true;
    }
}
