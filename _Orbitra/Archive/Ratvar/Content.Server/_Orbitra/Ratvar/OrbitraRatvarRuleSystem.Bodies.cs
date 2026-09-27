using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.NPC.Systems;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Roles;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarRuleSystem
{
    private static readonly Robust.Shared.Prototypes.EntProtoId MarauderRole = "OrbitraMindRoleRatvarMarauder";
    private static readonly Robust.Shared.Prototypes.EntProtoId BuilderRole = "OrbitraMindRoleRatvarCogscarab";
    [Dependency] private NpcFactionSystem _factions = default!;
    private const string CultFaction = "OrbitraRatvar";

    private void InitializeBodies()
    {
        SubscribeLocalEvent<MindContainerComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<OrbitraRatvarBodyComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<RoleAddedEvent>(OnMarauderRoleAdded);
        SubscribeLocalEvent<RoleRemovedEvent>(OnRoleRemoved);
    }

    private void OnRoleRemoved(RoleRemovedEvent args)
    {
        if (args.Mind.OwnedEntity is { } body && !TerminatingOrDeleted(body))
            RefreshBody(body);
    }

    private void RefreshCultBodies(OrbitraRatvarRuleComponent cult)
    {
        foreach (var mind in cult.Members)
        {
            if (TryComp<MindComponent>(mind, out var data) && data.OwnedEntity is { } body && !TerminatingOrDeleted(body))
                RefreshBody(body);
        }
    }

    private void OnMindAdded(Entity<MindContainerComponent> ent, ref MindAddedMessage args)
    {
        if (TryComp<OrbitraRatvarShellComponent>(ent, out var shell) && shell.Rule is { } owner &&
            TryComp<OrbitraRatvarRuleComponent>(owner, out var cult) && !cult.Won && !cult.Lost &&
            GameTicker.IsGameRuleActive(owner) && Living(ent) &&
            (shell.Builder ? HasComp<OrbitraRatvarCogscarabComponent>(ent) : HasComp<OrbitraRatvarMarauderComponent>(ent)) &&
            !_roles.MindIsAntagonist(args.Mind.Owner))
        {
            _roles.MindAddRole(args.Mind.Owner, shell.Builder ? BuilderRole : MarauderRole);
            BindMember((owner, cult), args.Mind.Owner);
        }
        RefreshBody(ent);
    }

    private void OnMarauderRoleAdded(RoleAddedEvent args)
    {
        if (!_roles.MindHasRole<OrbitraRatvarRoleComponent>(args.MindId, out var role) ||
            !(role.Value.Comp2.Marauder || role.Value.Comp2.Builder) || role.Value.Comp2.Rule != null ||
            args.Mind.OwnedEntity is not { } body) return;
        if (TryComp<OrbitraRatvarShellComponent>(body, out var shell) && Living(body) &&
            (shell.Builder ? HasComp<OrbitraRatvarCogscarabComponent>(body) : HasComp<OrbitraRatvarMarauderComponent>(body)) &&
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
