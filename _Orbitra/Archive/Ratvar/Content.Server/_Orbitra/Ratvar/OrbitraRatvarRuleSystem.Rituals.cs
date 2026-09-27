using System.Numerics;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Containers;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarRuleSystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityLookupSystem _submissionLookup = default!;

    private readonly HashSet<Entity<MobStateComponent>> _submissionCandidates = [];
    private TimeSpan _nextSubmissionScan;

    private void InitializeRituals()
    {
        SubscribeLocalEvent<OrbitraRatvarTabletComponent, AfterInteractEvent>(OnTabletInteract);
        SubscribeLocalEvent<OrbitraRatvarSubmissionComponent, OrbitraRatvarConversionEvent>(OnConversionFinished);
        SubscribeLocalEvent<OrbitraRatvarSubmissionComponent, DoAfterAttemptEvent<OrbitraRatvarConversionEvent>>(OnConversionAttempt);
    }

    private void OnTabletInteract(Entity<OrbitraRatvarTabletComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !HasComp<ActiveOrbitraRatvarEmpowermentComponent>(ent)) return;
        args.Handled = true;
        if (args.Target is { } target) TryTargetEmpowerment(ent, args.User, target);
    }

    private void OnConversionAttempt(Entity<OrbitraRatvarSubmissionComponent> ent, ref DoAfterAttemptEvent<OrbitraRatvarConversionEvent> args)
    {
        if (!ValidSubmissionContext(ent, args.DoAfter.Args.User)) args.Cancel();
    }

    private void OnConversionFinished(Entity<OrbitraRatvarSubmissionComponent> ent, ref OrbitraRatvarConversionEvent args)
    {
        if (args.Handled || ent.Comp.Target != args.User) return;
        args.Handled = true;
        if (!args.Cancelled && ValidSubmissionContext(ent, args.User)) TryConvertOnSigil(ent, args.User);
        ent.Comp.Target = null;
        ent.Comp.Mind = null;
        ent.Comp.Rule = null;
    }

    /// <summary>Periodically discovers nearby targets; ongoing rituals validate every server tick.</summary>
    private void UpdateSubmissionSigils()
    {
        if (Timing.CurTime < _nextSubmissionScan) return;
        _nextSubmissionScan = Timing.CurTime + TimeSpan.FromSeconds(0.25);
        var query = EntityQueryEnumerator<OrbitraRatvarSubmissionComponent, OrbitraRatvarStructureComponent>();
        while (query.MoveNext(out var uid, out var submission, out var structure))
        {
            // DoAfter хранится на цели: при её удалении событие завершения уже не придёт.
            if (submission.Target is { } previous && TerminatingOrDeleted(previous))
            {
                submission.Target = null;
                submission.Mind = null;
                submission.Rule = null;
            }
            if (!structure.ConversionSigil || submission.Target != null || !Transform(uid).Anchored) continue;
            _submissionCandidates.Clear();
            _submissionLookup.GetEntitiesInRange(Transform(uid).Coordinates, submission.Radius, _submissionCandidates);
            foreach (var target in _submissionCandidates)
                if (TryStartSubmission((uid, submission), target)) break;
        }
    }

    /// <summary>Starts a passive ritual without claiming the supporting cultist's tablet.</summary>
    public bool TryStartSubmission(Entity<OrbitraRatvarSubmissionComponent> sigil, EntityUid target)
    {
        if (sigil.Comp.Target != null || !CanConvertOnSigil(sigil, target, out var rule) ||
            !_mind.TryGetMind(target, out var mind, out _)) return false;
        sigil.Comp.Target = target;
        sigil.Comp.Mind = mind;
        sigil.Comp.Rule = rule.Owner;
        var args = new DoAfterArgs(EntityManager, target, rule.Comp.ConversionDelay,
            new OrbitraRatvarConversionEvent(), sigil, target: target)
        {
            // Печать действует на цель, а не требует добровольного действия или свободных рук.
            RequireCanInteract = false,
            BreakOnMove = false,
            BreakOnDamage = false,
            NeedHand = false,
            AttemptFrequency = AttemptFrequency.EveryTick,
            CancelDuplicate = false,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };
        if (_doAfter.TryStartDoAfter(args))
        {
            _popup.PopupEntity(Loc.GetString("orbitra-ratvar-submission-start"), target, target);
            return true;
        }
        sigil.Comp.Target = null;
        sigil.Comp.Mind = null;
        sigil.Comp.Rule = null;
        return false;
    }

    private bool ValidSubmissionContext(Entity<OrbitraRatvarSubmissionComponent> sigil, EntityUid target) =>
        sigil.Comp.Target == target && CanConvertOnSigil(sigil, target, out var rule) && rule.Owner == sigil.Comp.Rule &&
        _mind.TryGetMind(target, out var mind, out _) && mind == sigil.Comp.Mind;

    /// <summary>Grants membership only after revalidating the sigil, target and nearby support.</summary>
    public bool TryConvertOnSigil(Entity<OrbitraRatvarSubmissionComponent> sigil, EntityUid target)
    {
        if (!CanConvertOnSigil(sigil, target, out var rule) || !_mind.TryGetMind(target, out var mind, out _)) return false;
        _roles.MindAddRole(mind, CultRole);
        BindMember(rule, mind);
        rule.Comp.Converted.Add(mind);
        _adminLog.Add(LogType.Mind, LogImpact.High, $"Ratvar cult: {ToPrettyString(sigil)} converted {ToPrettyString(target)} ({ToPrettyString(mind)}).");
        return true;
    }

    /// <summary>Checks passive conversion eligibility independently of tablet spells and Vanguard.</summary>
    public bool CanConvertOnSigil(Entity<OrbitraRatvarSubmissionComponent> sigil, EntityUid target,
        out Entity<OrbitraRatvarRuleComponent> rule)
    {
        rule = default;
        if (TerminatingOrDeleted(sigil) || EntityManager.IsQueuedForDeletion(sigil) ||
            !TryComp<OrbitraRatvarStructureComponent>(sigil, out var structure) || !structure.ConversionSigil ||
            structure.Rule is not { } owner || !TryComp<OrbitraRatvarRuleComponent>(owner, out var cult) ||
            cult.Won || cult.Lost || !GameTicker.IsGameRuleActive(owner) || !Transform(sigil).Anchored ||
            _containers.IsEntityInContainer(sigil)) return false;
        rule = (owner, cult);
        if (TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target) || !Living(target) ||
            !TryComp<HumanoidProfileComponent>(target, out var humanoid) || humanoid.Species != "Human" ||
            !_mind.TryGetMind(target, out var mind, out _) || _roles.MindIsAntagonist(mind) ||
            TryComp<MindShieldStatusComponent>(target, out var shield) && shield.IsMindshielded ||
            _jobs.MindTryGetJobId(mind, out var job) && job == "Chaplain" ||
            cult.ProtectedUntil.TryGetValue(mind, out var until) && until > Timing.CurTime ||
            _containers.IsEntityInContainer(target) || !Near(sigil, target, sigil.Comp.Radius) ||
            !_interaction.InRangeUnobstructed(sigil.Owner, target, range: sigil.Comp.Radius)) return false;

        // Только индекс участников своего культа, без обхода всех игроков и табличек станции.
        foreach (var member in cult.Members)
        {
            if (!TryComp<MindComponent>(member, out var data) || data.CurrentEntity is not { } helper || helper == target ||
                !Living(helper) || !CanReciteInBody(helper) || !TryGetCult(helper, out var helperRule) || helperRule.Owner != owner ||
                _containers.IsEntityInContainer(helper) || !_blocker.CanInteract(helper, target) ||
                !Near(helper, sigil, cult.RitualRange) || !_interaction.InRangeUnobstructed(helper, target)) continue;
            return true;
        }
        return false;
    }

    private bool Near(EntityUid first, EntityUid second, float range)
    {
        var a = _transform.GetMapCoordinates(first);
        var b = _transform.GetMapCoordinates(second);
        return a.MapId == b.MapId && Vector2.DistanceSquared(a.Position, b.Position) <= range * range;
    }

    private bool CanReciteInBody(EntityUid user) =>
        TryComp<HumanoidProfileComponent>(user, out var profile) && profile.Species == "Human" &&
        _mind.TryGetMind(user, out var mind, out _) &&
        _roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role) &&
        !role.Value.Comp2.Marauder && !role.Value.Comp2.Builder && !role.Value.Comp2.Eminence;

    private bool SameRitualMind(OrbitraRatvarTabletComponent tablet, EntityUid user) =>
        _mind.TryGetMind(user, out var mind, out _) && tablet.RitualMind == mind;
}
