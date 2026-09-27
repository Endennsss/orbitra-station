using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarEminenceSystem
{
    // Призыв обычного раунда использует тот же единственный резерв, что и тестовая гостроль.
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    private readonly Dictionary<EntityUid, EntityUid> _summons = [];

    private void InitializeSummoning()
    {
        SubscribeLocalEvent<OrbitraRatvarEminenceSpireComponent, InteractHandEvent>(OnSpireUse);
        SubscribeLocalEvent<OrbitraRatvarEminenceSpireComponent, ExaminedEvent>(OnSpireExamine);
        SubscribeLocalEvent<OrbitraRatvarEminenceSpireComponent, ComponentShutdown>(OnSpireShutdown);
    }

    private void OnSpireUse(Entity<OrbitraRatvarEminenceSpireComponent> ent, ref InteractHandEvent args)
    {
        if (!args.Handled) args.Handled = TrySummon(ent, args.User);
    }

    private void OnSpireExamine(Entity<OrbitraRatvarEminenceSpireComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !_ratvarRule.TryGetCult(args.Examiner, out var cult) || !ValidSpire(ent, cult)) return;
        args.PushMarkup(Loc.GetString(ent.Comp.SummonAt != null ? "orbitra-ratvar-eminence-summon-pending" :
            _reservations.ContainsKey(cult) || _invitations.ContainsKey(cult) ? "orbitra-ratvar-eminence-summon-occupied" :
            "orbitra-ratvar-eminence-summon-idle"));
    }

    private void OnSpireShutdown(Entity<OrbitraRatvarEminenceSpireComponent> ent, ref ComponentShutdown args) => CancelSummon(ent);

    /// <summary>Starts the objection period or cancels the pending proposal. It never grants a role directly.</summary>
    public bool TrySummon(Entity<OrbitraRatvarEminenceSpireComponent> spire, EntityUid user)
    {
        if (!CanSummon(spire, user, out var rule))
        {
            _popup.PopupEntity(Loc.GetString("orbitra-ratvar-eminence-summon-denied"), spire, user);
            return false;
        }
        if (spire.Comp.SummonAt != null)
        {
            CancelSummon(spire);
            _ratvarRule.SendCultNotice(rule, Loc.GetString("orbitra-ratvar-eminence-summon-cancelled"));
            return true;
        }
        spire.Comp.PendingRule = rule;
        spire.Comp.SummonAt = _timing.CurTime + spire.Comp.ObjectionPeriod;
        _summons.Add(rule, spire);
        _ratvarRule.SendCultNotice(rule, Loc.GetString("orbitra-ratvar-eminence-summon-started",
            ("seconds", (int) spire.Comp.ObjectionPeriod.TotalSeconds)));
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"Ratvar Eminence: {ToPrettyString(user)} proposed summon at {ToPrettyString(spire)} for {ToPrettyString(rule)}.");
        return true;
    }

    /// <summary>Only a controlled living member can interact with its anchored station beacon.</summary>
    public bool CanSummon(Entity<OrbitraRatvarEminenceSpireComponent> spire, EntityUid user, out EntityUid rule)
    {
        rule = default;
        if (!_ratvarRule.TryGetCult(user, out var cult) || !ValidSpire(spire, cult) ||
            !_mind.TryGetMind(user, out var mind, out _) || !ControlledBy(mind, user) ||
            !TryComp<MobStateComponent>(user, out var mob) || mob.CurrentState != MobState.Alive ||
            _containers.IsEntityInContainer(user) || !_interaction.InRangeUnobstructed(user, spire.Owner)) return false;
        rule = cult;
        return !_reservations.ContainsKey(rule) && !_invitations.ContainsKey(rule) &&
            (!_summons.TryGetValue(rule, out var pending) || pending == spire.Owner);
    }

    private bool ValidSpire(EntityUid spire, EntityUid rule) => ValidInvitationCult(rule) &&
        !TerminatingOrDeleted(spire) && !EntityManager.IsQueuedForDeletion(spire) &&
        HasComp<OrbitraRatvarEminenceSpireComponent>(spire) &&
        TryComp<OrbitraRatvarStructureComponent>(spire, out var structure) && structure.Rule == rule &&
        Transform(spire).Anchored && !_containers.IsEntityInContainer(spire) &&
        Comp<OrbitraRatvarRuleComponent>(rule).Station is { } station &&
        _station.GetOwningStation(spire) == station && _station.GetLargestGrid(station) is { } grid &&
        Transform(spire).GridUid == grid;

    private void CancelSummon(Entity<OrbitraRatvarEminenceSpireComponent> spire)
    {
        if (spire.Comp.PendingRule is { } rule && _summons.TryGetValue(rule, out var pending) && pending == spire.Owner)
            _summons.Remove(rule);
        spire.Comp.SummonAt = null;
        spire.Comp.PendingRule = null;
    }

    private void UpdateSummoning()
    {
        var query = EntityQueryEnumerator<OrbitraRatvarEminenceSpireComponent>();
        while (query.MoveNext(out var uid, out var spire))
        {
            if (spire.SummonAt is not { } deadline || spire.PendingRule is not { } rule) continue;
            if (!ValidSpire(uid, rule) || _reservations.ContainsKey(rule) || _invitations.ContainsKey(rule))
            {
                CancelSummon((uid, spire));
                continue;
            }
            if (_timing.CurTime < deadline) continue;
            CancelSummon((uid, spire));
            var invitation = Spawn(InvitationPrototype, Transform(uid).Coordinates);
            var pending = Comp<OrbitraRatvarEminenceInvitationComponent>(invitation);
            pending.Rule = rule;
            pending.TestOnly = false;
            pending.Spire = uid;
            pending.ExpiresAt = _timing.CurTime + spire.InvitationDuration;
            _invitations.Add(rule, invitation);
            EntityManager.AddComponents(invitation, pending.RoleComponents);
            _ratvarRule.SendCultNotice(rule, Loc.GetString("orbitra-ratvar-eminence-summon-invited"));
        }
    }
}
