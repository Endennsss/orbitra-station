using Content.Server.GameTicking.Rules;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Body.Components;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Server.RoundEnd;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Owns cult membership and the shared economy for a single round rule.</summary>
public sealed partial class OrbitraRatvarRuleSystem : GameRuleSystem<OrbitraRatvarRuleComponent>
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private RoleSystem _roles = default!;
    [Dependency] private JobSystem _jobs = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    private static readonly EntProtoId CultRole = "OrbitraMindRoleRatvar";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<OrbitraRatvarRoleComponent, GetBriefingEvent>(OnBriefing);
        SubscribeLocalEvent<OrbitraRatvarBodyComponent, SolutionChangedEvent>(OnSolutionChanged);
        InitializeRituals();
        InitializeScriptures();
        InitializeArk();
        InitializeBodies();
        InitializeDefences();
    }

    private void OnSolutionChanged(Entity<OrbitraRatvarBodyComponent> ent, ref SolutionChangedEvent args)
    {
        if (!TryComp<BloodstreamComponent>(ent, out var bloodstream) ||
            args.Solution.Comp.Id != bloodstream.BloodSolutionName || !TryGetCult(ent, out var rule) ||
            !_mind.TryGetMind(ent, out var mind, out _)) return;
        if (args.Solution.Comp.Solution.GetTotalPrototypeQuantity(rule.Comp.PurifyingReagent) <= 0)
            rule.Comp.HolyWaterSince.Remove(mind);
    }

    private void OnBriefing(Entity<OrbitraRatvarRoleComponent> ent, ref GetBriefingEvent args)
    {
        args.Append(Loc.GetString("orbitra-ratvar-briefing"));
    }

    private void OnAntagSelected(Entity<OrbitraRatvarRuleComponent> rule, ref AfterAntagEntitySelectedEvent args)
    {
        if (!_mind.TryGetMind(args.EntityUid, out var mind, out _)) return;
        BindMember(rule, mind);
        rule.Comp.Station ??= _station.GetOwningStation(args.EntityUid);
    }

    private void BindMember(Entity<OrbitraRatvarRuleComponent> rule, EntityUid mind)
    {
        if (!_roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role)) return;
        role.Value.Comp2.Rule = rule.Owner;
        rule.Comp.Members.Add(mind);
        if (TryComp<MindComponent>(mind, out var data) && data.OwnedEntity is { } body) RefreshBody(body);
    }

    protected override void Started(EntityUid uid, OrbitraRatvarRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        component.StartedAt = Timing.CurTime;
        component.Energy = component.StartingEnergy;
    }

    protected override void Ended(EntityUid uid, OrbitraRatvarRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        // Роль сознания остаётся для итогов раунда, но не даёт телу действующую принадлежность.
        foreach (var mind in component.Members)
        {
            if (TryComp<MindComponent>(mind, out var data) && data.OwnedEntity is { } body)
                RefreshBody(body);
        }
        component.HolyWaterSince.Clear();
    }

    protected override void ActiveTick(EntityUid uid, OrbitraRatvarRuleComponent rule, GameRuleComponent gameRule, float frameTime)
    {
        UpdateArk((uid, rule));
        if (rule.NextUpdate > Timing.CurTime || rule.Won || rule.Lost) return;
        rule.NextUpdate = Timing.CurTime + TimeSpan.FromSeconds(1);
        RefreshTablets();
        foreach (var mind in rule.Members)
        {
            if (!TryComp<MindComponent>(mind, out var data) || data.OwnedEntity is not { } body ||
                !_roles.MindHasRole<OrbitraRatvarRoleComponent>(mind))
            {
                rule.HolyWaterSince.Remove(mind);
                continue;
            }
            RefreshBody(body);
            var wet = TryComp<BloodstreamComponent>(body, out var bloodstream) &&
                _solutions.TryGetSolution(body, bloodstream.BloodSolutionName, out _, out var solution) &&
                solution.GetTotalPrototypeQuantity(rule.PurifyingReagent) > 0;
            if (!wet) { rule.HolyWaterSince.Remove(mind); continue; }
            if (!rule.HolyWaterSince.TryGetValue(mind, out var since))
                rule.HolyWaterSince[mind] = Timing.CurTime;
            else if (Timing.CurTime - since >= rule.PurificationDelay)
                TryPurify((uid, rule), mind);
        }
    }

    protected override void AppendRoundEndText(EntityUid uid, OrbitraRatvarRuleComponent component,
        GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        args.AddLine(Loc.GetString(component.Won ? "orbitra-ratvar-victory" : "orbitra-ratvar-defeat"));
        args.AddLine(Loc.GetString("orbitra-ratvar-round-statistics", ("count", component.Converted.Count)));
    }

    /// <summary>Persistent, unique conversions and generated energy unlock scripture tiers.</summary>
    public int GetTier(OrbitraRatvarRuleComponent rule)
    {
        if (rule.Converted.Count >= rule.TierThreeConverts && rule.Generated >= rule.TierThreeEnergy) return 3;
        if (rule.Converted.Count >= rule.TierTwoConverts && rule.Generated >= rule.TierTwoEnergy) return 2;
        return 1;
    }

    /// <summary>Resolve allegiance through the current body's mind, not a transferable body marker.</summary>
    public bool TryGetCult(EntityUid body, out Entity<OrbitraRatvarRuleComponent> rule)
    {
        rule = default;
        if (!_mind.TryGetMind(body, out var mind, out _) ||
            !_roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role) ||
            role.Value.Comp2.Rule is not { } owner || !TryComp<OrbitraRatvarRuleComponent>(owner, out var comp) ||
            comp.Won || comp.Lost || !GameTicker.IsGameRuleActive(owner)) return false;
        rule = (owner, comp);
        return true;
    }

    public bool TryPurify(Entity<OrbitraRatvarRuleComponent> rule, EntityUid mind)
    {
        if (!CanPurify(rule, mind)) return false;
        _roles.MindRemoveRole<OrbitraRatvarRoleComponent>(mind);
        if (TryComp<MindComponent>(mind, out var data) && data.OwnedEntity is { } body) RefreshBody(body);
        rule.Comp.HolyWaterSince.Remove(mind);
        rule.Comp.ProtectedUntil[mind] = Timing.CurTime + rule.Comp.ConversionImmunity;
        _adminLog.Add(LogType.Mind, LogImpact.Medium, $"Ratvar cult: {ToPrettyString(mind)} purified by holy water.");
        return true;
    }

    public bool CanPurify(Entity<OrbitraRatvarRuleComponent> rule, EntityUid mind) =>
        _roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role) && role.Value.Comp2.Rule == rule.Owner;

    private bool Living(EntityUid body) => TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState != MobState.Dead;
}
