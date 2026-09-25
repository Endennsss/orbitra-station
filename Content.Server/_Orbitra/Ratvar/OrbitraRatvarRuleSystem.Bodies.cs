using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.NPC.Systems;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Roles;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarRuleSystem
{
    [Dependency] private NpcFactionSystem _factions = default!;
    private const string CultFaction = "OrbitraRatvar";

    private void InitializeBodies()
    {
        SubscribeLocalEvent<MindContainerComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<OrbitraRatvarBodyComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<RoleAddedEvent>(OnMarauderRoleAdded);
    }

    private void OnMindAdded(Entity<MindContainerComponent> ent, ref MindAddedMessage args)
    {
        if (TryComp<OrbitraRatvarShellComponent>(ent, out var shell) && shell.Rule is { } owner &&
            TryComp<OrbitraRatvarRuleComponent>(owner, out var cult) && !cult.Won && !cult.Lost &&
            GameTicker.IsGameRuleActive(owner) && HasComp<OrbitraRatvarMarauderComponent>(ent) &&
            !_roles.MindIsAntagonist(args.Mind.Owner))
        {
            _roles.MindAddRole(args.Mind.Owner, "OrbitraMindRoleRatvarMarauder");
            BindMember((owner, cult), args.Mind.Owner);
        }
        RefreshBody(ent);
    }

    private void OnMarauderRoleAdded(RoleAddedEvent args)
    {
        if (!_roles.MindHasRole<OrbitraRatvarRoleComponent>(args.MindId, out var role) ||
            !role.Value.Comp2.Marauder || role.Value.Comp2.Rule != null || args.Mind.OwnedEntity is not { } body) return;
        if (TryComp<OrbitraRatvarShellComponent>(body, out var shell) && HasComp<OrbitraRatvarMarauderComponent>(body) &&
            shell.Rule is { } owner && TryComp<OrbitraRatvarRuleComponent>(owner, out var cult) &&
            !cult.Won && !cult.Lost && GameTicker.IsGameRuleActive(owner))
        {
            BindMember((owner, cult), args.MindId);
        }
    }

    private void OnMindRemoved(Entity<OrbitraRatvarBodyComponent> ent, ref MindRemovedMessage args)
    {
        _factions.RemoveFaction(ent.Owner, CultFaction);
        RemComp<OrbitraRatvarBodyComponent>(ent);
        if (_roles.MindHasRole<OrbitraRatvarRoleComponent>(args.Mind.Owner, out var role) &&
            role.Value.Comp2.Rule is { } owner && TryComp<OrbitraRatvarRuleComponent>(owner, out var rule))
            rule.HolyWaterSince.Remove(args.Mind.Owner);
    }

    private void RefreshBody(EntityUid body)
    {
        if (TryGetCult(body, out _))
        {
            EnsureComp<OrbitraRatvarBodyComponent>(body);
            _factions.AddFaction(body, CultFaction);
        }
        else if (HasComp<OrbitraRatvarBodyComponent>(body))
        {
            _factions.RemoveFaction(body, CultFaction);
            RemComp<OrbitraRatvarBodyComponent>(body);
        }
    }
}
