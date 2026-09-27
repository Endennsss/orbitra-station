using Content.Server.Construction.Components;
using Content.Server.Wires;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Access.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Lock;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarFabricatorSystem
{
    private void InitializeDoors()
    {
        SubscribeLocalEvent<OrbitraRatvarFabricatorComponent, OrbitraRatvarDoorEvent>(OnDoorFinished);
        SubscribeLocalEvent<OrbitraRatvarFabricatorComponent, DoAfterAttemptEvent<OrbitraRatvarDoorEvent>>(OnDoorAttempt);
    }

    private void OnDoorAttempt(Entity<OrbitraRatvarFabricatorComponent> ent, ref DoAfterAttemptEvent<OrbitraRatvarDoorEvent> args)
    {
        if (args.DoAfter.Args.Target is not { } target ||
            !CanConvertDoor(ent, args.DoAfter.Args.User, target, (OrbitraRatvarDoorEvent) args.DoAfter.Args.Event, out _))
            args.Cancel();
    }

    private void OnDoorFinished(Entity<OrbitraRatvarFabricatorComponent> ent, ref OrbitraRatvarDoorEvent args)
    {
        if (args.Target is { } target && TryComp<OrbitraRatvarRepairTargetComponent>(target, out var operations))
            operations.Pending.Remove(args.DoAfter.Id);
        if (args.Handled || ent.Comp.Pending != args.DoAfter.Id)
            return;
        args.Handled = true;
        ent.Comp.Pending = null;
        if (!args.Cancelled && args.Target is { } door)
            TryFinishDoor(ent, args.User, door, args);
    }

    /// <summary>Starts an explicitly supported closed door conversion without consuming resources.</summary>
    public bool TryStartDoor(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, EntityUid target)
    {
        if (tool.Comp.Pending != null || !_cult.TryGetCult(user, out var rule) ||
            !_mind.TryGetMind(user, out var mind, out _) || Transform(user).GridUid is not { } grid ||
            Transform(grid).MapUid is not { } map)
            return false;
        var context = new OrbitraRatvarDoorEvent
        {
            Mind = GetNetEntity(mind), Rule = GetNetEntity(rule.Owner),
            Grid = GetNetEntity(grid), Map = GetNetEntity(map),
        };
        if (!CanConvertDoor(tool, user, target, context, out _))
            return false;
        var operation = new DoAfterArgs(EntityManager, user, tool.Comp.DoorDelay, context, tool, target: target, used: tool)
        {
            BreakOnMove = true, BreakOnDamage = true, BreakOnHandChange = true, NeedHand = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };
        if (!_doAfter.TryStartDoAfter(operation, out var id))
            return false;
        tool.Comp.Pending = id;
        // Отмена до удаления Transform цели общая для ремонта и преобразований.
        if (id is { } pending && _doAfter.IsRunning(pending))
            EnsureComp<OrbitraRatvarRepairTargetComponent>(target).Pending.Add(pending);
        return true;
    }

    private bool CanConvertDoor(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, EntityUid target,
        OrbitraRatvarDoorEvent context, out Entity<OrbitraRatvarRuleComponent> rule)
    {
        rule = default;
        if (TerminatingOrDeleted(tool) || TerminatingOrDeleted(user) || TerminatingOrDeleted(target) ||
            tool.Comp.DoorEnergy < 0 || tool.Comp.DoorDelay <= TimeSpan.Zero ||
            !_cult.TryGetCult(user, out rule) || rule.Owner != GetEntity(context.Rule) ||
            rule.Comp.Energy < tool.Comp.DoorEnergy || !_mind.TryGetMind(user, out var mind, out _) ||
            mind != GetEntity(context.Mind) || !TryComp<MobStateComponent>(user, out var mob) || mob.CurrentState != MobState.Alive ||
            !_hands.IsHolding(user, tool) || !_blocker.CanInteract(user, target) ||
            _container.IsEntityInContainer(user) || _container.IsEntityInContainer(target) ||
            Prototype(target) is not { } prototype)
            return false;

        var airlock = tool.Comp.Airlocks.TryGetValue(new EntProtoId(prototype.ID), out var sourceNode);
        if (!airlock && !tool.Comp.Doors.TryGetValue(new EntProtoId(prototype.ID), out sourceNode))
            return false;
        if (!TryComp<DoorComponent>(target, out var door) || door.State != DoorState.Closed || HasComp<LockComponent>(target))
            return false;

        var transform = Transform(target);
        if (!transform.Anchored ||
            transform.GridUid != GetEntity(context.Grid) || Transform(user).GridUid != transform.GridUid ||
            transform.MapUid != GetEntity(context.Map) || _damage.GetTotalDamage(target) != 0 ||
            !TryComp<ConstructionComponent>(target, out var construction) ||
            construction.Graph.Id != (airlock ? "Airlock" : "DoorGraph") || construction.Node != sourceNode ||
            construction.TargetNode != null || construction.InteractionQueue.Count != 0)
            return false;

        if (airlock)
        {
            // Штатная замена графа переносит плату; произвольные контейнеры и дочерние сущности не допускаются.
            if (!HasComp<AirlockComponent>(target) ||
                !_container.TryGetContainer(target, "board", out var board) ||
                transform.ChildCount != board.ContainedEntities.Count ||
                TryComp<DoorBoltComponent>(target, out var bolts) && bolts.BoltsDown)
                return false;
            foreach (var container in _container.GetAllContainers(target))
            {
                if (container.ID != "board")
                    return false;
            }
        }
        else if (transform.ChildCount != 0 || HasComp<ContainerManagerComponent>(target) ||
                 HasComp<AirlockComponent>(target) || HasComp<AccessReaderComponent>(target) || HasComp<WiresComponent>(target))
            return false;
        return _interaction.InRangeUnobstructed(user, target);
    }

    private bool TryFinishDoor(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, EntityUid target,
        OrbitraRatvarDoorEvent context)
    {
        if (!CanConvertDoor(tool, user, target, context, out var rule))
            return false;
        if (HasComp<AirlockComponent>(target))
            _construction.AddContainer(target, "board");
        rule.Comp.Energy -= tool.Comp.DoorEnergy;
        if (!_construction.ChangeGraph(target, user, "OrbitraRatvarDoor", "door", performActions: false))
        {
            rule.Comp.Energy += tool.Comp.DoorEnergy;
            return false;
        }
        _adminLog.Add(LogType.Construction, LogImpact.Medium,
            $"Ratvar cult: {ToPrettyString(user)} converted {ToPrettyString(target)} to OrbitraRatvarDoor, spent {tool.Comp.DoorEnergy} from {ToPrettyString(rule)}.");
        _popup.PopupEntity(Loc.GetString("orbitra-ratvar-fabricator-door-done"), tool, user);
        return true;
    }
}
