using Content.Shared.Database;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarFabricatorSystem
{
    // Обмен только разрешённых листов без удаления их нечётного остатка и без выдачи энергии.

    /// <summary>Consumes whole pairs of supported sheets and produces brass without awarding energy or progress.</summary>
    public bool TryRecycleSheets(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, EntityUid target)
    {
        if (!CanRecycleSheets(tool, user, target, out var stack, out var count))
            return false;

        var consumed = count * tool.Comp.RecyclingRatio;
        var coordinates = Transform(target).Coordinates;
        // TryUse вызывает события изменения стопки; задержку выставляем до этих событий.
        tool.Comp.NextBrassProduction = _timing.CurTime + tool.Comp.BrassCooldown;
        if (!_stack.TryUse((target, stack), consumed))
            return false;

        _stack.SpawnMultipleAtPosition(tool.Comp.BrassPrototype, count, coordinates);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"Ratvar cult: {ToPrettyString(user)} recycled {consumed} sheets from {ToPrettyString(target)} into {count} brass with {ToPrettyString(tool)}.");
        _popup.PopupEntity(Loc.GetString("orbitra-ratvar-fabricator-recycle-done", ("consumed", consumed), ("count", count)), tool, user);
        return true;
    }

    private bool CanRecycleSheets(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, EntityUid target,
        out StackComponent stack, out int count)
    {
        stack = default!;
        count = 0;
        if (TerminatingOrDeleted(tool) || TerminatingOrDeleted(user) || TerminatingOrDeleted(target) ||
            EntityManager.IsQueuedForDeletion(tool) || EntityManager.IsQueuedForDeletion(user) || EntityManager.IsQueuedForDeletion(target) ||
            tool.Comp.Pending != null || tool.Comp.NextBrassProduction > _timing.CurTime ||
            tool.Comp.RecyclingRatio <= 0 || tool.Comp.BrassBatchSize <= 0 || tool.Comp.BrassCooldown <= TimeSpan.Zero ||
            !_cult.TryGetCult(user, out _) || !TryComp<MobStateComponent>(user, out var mob) || mob.CurrentState != MobState.Alive ||
            !_hands.IsHolding(user, tool) || !_blocker.CanUseHeldEntity(user, tool) || !_blocker.CanInteract(user, target) ||
            _container.IsEntityInContainer(user) || _container.IsEntityInContainer(target) ||
            Transform(user).GridUid is not { } grid || Transform(target).GridUid != grid || Transform(target).Anchored ||
            !_interaction.InRangeUnobstructed(user, target) || Transform(target).ChildCount != 0 ||
            Prototype(target) is not { } prototype || !tool.Comp.RecyclableSheets.Contains(new EntProtoId(prototype.ID)) ||
            !TryComp<StackComponent>(target, out var sourceStack) || sourceStack.Unlimited)
            return false;

        if (TryComp<ContainerManagerComponent>(target, out var containers))
        {
            foreach (var container in containers.Containers.Values)
            {
                if (container.Count != 0)
                    return false;
            }
        }

        stack = sourceStack;
        count = Math.Min(tool.Comp.BrassBatchSize, stack.Count / tool.Comp.RecyclingRatio);
        return count > 0;
    }
}
