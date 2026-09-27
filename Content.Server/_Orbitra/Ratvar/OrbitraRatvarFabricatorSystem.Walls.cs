using Content.Server.Construction;
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
    [Dependency] private ConstructionSystem _construction = default!;

    private void InitializeWalls()
    {
        SubscribeLocalEvent<OrbitraRatvarFabricatorComponent, OrbitraRatvarWallEvent>(OnWallFinished);
        SubscribeLocalEvent<OrbitraRatvarFabricatorComponent, DoAfterAttemptEvent<OrbitraRatvarWallEvent>>(OnWallAttempt);
    }

    private void OnWallAttempt(Entity<OrbitraRatvarFabricatorComponent> ent, ref DoAfterAttemptEvent<OrbitraRatvarWallEvent> args)
    {
        if (args.DoAfter.Args.Target is not { } target ||
            !CanConvertWall(ent, args.DoAfter.Args.User, target, (OrbitraRatvarWallEvent) args.DoAfter.Args.Event, out _))
            args.Cancel();
    }

    private void OnWallFinished(Entity<OrbitraRatvarFabricatorComponent> ent, ref OrbitraRatvarWallEvent args)
    {
        if (args.Target is { } target && TryComp<OrbitraRatvarRepairTargetComponent>(target, out var operations))
            operations.Pending.Remove(args.DoAfter.Id);
        if (args.Handled || ent.Comp.Pending != args.DoAfter.Id)
            return;
        args.Handled = true;
        ent.Comp.Pending = null;
        if (!args.Cancelled && args.Target is { } wall)
            TryFinishWall(ent, args.User, wall, args);
    }

    /// <summary>Starts the explicit Girder wall adapter without consuming resources.</summary>
    public bool TryStartWall(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, EntityUid target)
    {
        if (tool.Comp.Pending != null || !_cult.TryGetCult(user, out var rule) ||
            !_mind.TryGetMind(user, out var mind, out _) || Transform(user).GridUid is not { } grid ||
            Transform(grid).MapUid is not { } map)
            return false;
        var context = new OrbitraRatvarWallEvent
        {
            Mind = GetNetEntity(mind), Rule = GetNetEntity(rule.Owner),
            Grid = GetNetEntity(grid), Map = GetNetEntity(map),
        };
        if (!CanConvertWall(tool, user, target, context, out _))
            return false;
        var operation = new DoAfterArgs(EntityManager, user, tool.Comp.WallDelay, context, tool, target: target, used: tool)
        {
            BreakOnMove = true, BreakOnDamage = true, BreakOnHandChange = true, NeedHand = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };
        if (!_doAfter.TryStartDoAfter(operation, out var id))
            return false;
        tool.Comp.Pending = id;
        // Общий список отменяет операции до удаления Transform цели, как в ремонте.
        if (id is { } pending && _doAfter.IsRunning(pending))
            EnsureComp<OrbitraRatvarRepairTargetComponent>(target).Pending.Add(pending);
        return true;
    }

    private bool CanConvertWall(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, EntityUid target,
        OrbitraRatvarWallEvent context, out Entity<OrbitraRatvarRuleComponent> rule)
    {
        rule = default;
        if (TerminatingOrDeleted(tool) || TerminatingOrDeleted(user) || TerminatingOrDeleted(target) ||
            tool.Comp.WallEnergy < 0 || tool.Comp.WallDelay <= TimeSpan.Zero ||
            !_cult.TryGetCult(user, out rule) || rule.Owner != GetEntity(context.Rule) ||
            rule.Comp.Energy < tool.Comp.WallEnergy || !_mind.TryGetMind(user, out var mind, out _) ||
            mind != GetEntity(context.Mind) || !TryComp<MobStateComponent>(user, out var mob) || mob.CurrentState != MobState.Alive ||
            !_hands.IsHolding(user, tool) || !_blocker.CanInteract(user, target) ||
            _container.IsEntityInContainer(user) || _container.IsEntityInContainer(target) ||
            Prototype(target) is not { } prototype || !tool.Comp.Walls.TryGetValue(new EntProtoId(prototype.ID), out var sourceNode))
            return false;

        var transform = Transform(target);
        if (!transform.Anchored || transform.ChildCount != 0 || HasComp<ContainerManagerComponent>(target) ||
            transform.GridUid != GetEntity(context.Grid) || Transform(user).GridUid != transform.GridUid ||
            transform.MapUid != GetEntity(context.Map) || _damage.GetTotalDamage(target) != 0 ||
            !TryComp<ConstructionComponent>(target, out var construction) ||
            construction.Graph.Id != "Girder" || construction.Node != sourceNode ||
            construction.TargetNode != null || construction.InteractionQueue.Count != 0)
            return false;
        return _interaction.InRangeUnobstructed(user, target);
    }

    private bool TryFinishWall(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, EntityUid target,
        OrbitraRatvarWallEvent context)
    {
        if (!CanConvertWall(tool, user, target, context, out var rule))
            return false;
        rule.Comp.Energy -= tool.Comp.WallEnergy;
        // Штатная замена сначала создаёт новую стену и лишь затем удаляет старую.
        if (!_construction.ChangeNode(target, user, "brassWall", performActions: false))
        {
            rule.Comp.Energy += tool.Comp.WallEnergy;
            return false;
        }
        _adminLog.Add(LogType.Construction, LogImpact.Medium,
            $"Ratvar cult: {ToPrettyString(user)} converted {ToPrettyString(target)} to WallBrass, spent {tool.Comp.WallEnergy} from {ToPrettyString(rule)}.");
        _popup.PopupEntity(Loc.GetString("orbitra-ratvar-fabricator-wall-done"), tool, user);
        return true;
    }
}
