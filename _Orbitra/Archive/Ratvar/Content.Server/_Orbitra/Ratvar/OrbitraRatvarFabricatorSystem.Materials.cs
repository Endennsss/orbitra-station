using Content.Server.Stack;
using Content.Shared.Database;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarFabricatorSystem
{
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private IGameTiming _timing = default!;

    private void InitializeMaterials()
    {
        SubscribeLocalEvent<OrbitraRatvarFabricatorComponent, UseInHandEvent>(OnProduceBrass);
    }

    private void OnProduceBrass(Entity<OrbitraRatvarFabricatorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        if (!TryProduceBrass(ent, args.User))
            _popup.PopupEntity(Loc.GetString("orbitra-ratvar-fabricator-brass-denied", ("energy", ent.Comp.BrassEnergy)), ent, args.User);
    }

    /// <summary>Produces one affordable batch, charging only the current user's cult.</summary>
    public bool TryProduceBrass(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user)
    {
        if (!CanProduceBrass(tool, user, out var rule, out var count))
            return false;

        // Резервируем расход и задержку до спавна, который может вызвать другие события.
        var cost = count * tool.Comp.BrassEnergy;
        rule.Comp.Energy -= cost;
        tool.Comp.NextBrassProduction = _timing.CurTime + tool.Comp.BrassCooldown;
        _stack.SpawnMultipleAtPosition(tool.Comp.BrassPrototype, count, Transform(user).Coordinates);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"Ratvar cult: {ToPrettyString(user)} fabricated {count} brass with {ToPrettyString(tool)}, spent {cost} from {ToPrettyString(rule)}.");
        _popup.PopupEntity(Loc.GetString("orbitra-ratvar-fabricator-brass-done", ("count", count), ("energy", cost)), tool, user);
        return true;
    }

    private bool CanProduceBrass(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user,
        out Entity<OrbitraRatvarRuleComponent> rule, out int count)
    {
        rule = default;
        count = 0;
        if (TerminatingOrDeleted(tool) || TerminatingOrDeleted(user) ||
            EntityManager.IsQueuedForDeletion(tool) || EntityManager.IsQueuedForDeletion(user) ||
            tool.Comp.Pending != null || tool.Comp.NextBrassProduction > _timing.CurTime ||
            tool.Comp.BrassEnergy <= 0 || tool.Comp.BrassBatchSize <= 0 || tool.Comp.BrassCooldown <= TimeSpan.Zero ||
            !_cult.TryGetCult(user, out rule) || rule.Comp.Energy < tool.Comp.BrassEnergy ||
            !TryComp<MobStateComponent>(user, out var mob) || mob.CurrentState != MobState.Alive ||
            !_hands.IsHolding(user, tool) || !_blocker.CanUseHeldEntity(user, tool) ||
            _container.IsEntityInContainer(user) || Transform(user).GridUid == null)
            return false;

        count = Math.Min(tool.Comp.BrassBatchSize, rule.Comp.Energy / tool.Comp.BrassEnergy);
        return count > 0;
    }
}
