using Content.Server.Chat.Managers;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._Orbitra.Ratvar;
using System.Linq;
using Content.Shared.Database;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Mind;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarRuleSystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private DamageableSystem _damage = default!;

    private void InitializeScriptures()
    {
        SubscribeLocalEvent<OrbitraRatvarTabletComponent, ActivatableUIOpenAttemptEvent>(OnTabletOpenAttempt);
        SubscribeLocalEvent<OrbitraRatvarTabletComponent, BeforeActivatableUIOpenEvent>(OnTabletOpen);
        SubscribeLocalEvent<OrbitraRatvarTabletComponent, OrbitraRatvarScriptureEvent>(OnScriptureFinished);
        Subs.BuiEvents<OrbitraRatvarTabletComponent>(OrbitraRatvarUiKey.Key, subs =>
        {
            subs.Event<OrbitraRatvarScriptureMessage>(OnScriptureMessage);
            subs.Event<OrbitraRatvarCommunicateMessage>(OnCommunicate);
        });
    }

    private void OnTabletOpenAttempt(Entity<OrbitraRatvarTabletComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!CanUseTablet(ent, args.User, out _)) args.Cancel();
    }

    private void OnTabletOpen(Entity<OrbitraRatvarTabletComponent> ent, ref BeforeActivatableUIOpenEvent args) =>
        UpdateTablet(ent, args.User);

    private void OnScriptureMessage(Entity<OrbitraRatvarTabletComponent> ent, ref OrbitraRatvarScriptureMessage args)
    {
        TryStartScripture(ent, args.Actor, args.Scripture);
        UpdateTablet(ent, args.Actor);
    }

    private void OnCommunicate(Entity<OrbitraRatvarTabletComponent> ent, ref OrbitraRatvarCommunicateMessage args)
    {
        TryCommunicate(ent, args.Actor, args.Text);
    }

    /// <summary>Sends a validated message only to minds belonging to this tablet user's cult.</summary>
    public bool TryCommunicate(Entity<OrbitraRatvarTabletComponent> ent, EntityUid actor, string? text)
    {
        if (!CanCommunicate(ent, actor, text, out var rule)) return false;
        if (!TrySendHiveMessage(actor, text)) return false;
        ent.Comp.NextMessage = Timing.CurTime + ent.Comp.MessageCooldown;
        return true;
    }

    public bool CanCommunicate(Entity<OrbitraRatvarTabletComponent> ent, EntityUid actor, string? text,
        out Entity<OrbitraRatvarRuleComponent> rule)
    {
        return CanUseTablet(ent, actor, out rule) && ent.Comp.NextMessage <= Timing.CurTime &&
            !string.IsNullOrWhiteSpace(text) && text.Length <= 300 && !text.Contains('\n') && !text.Contains('\r');
    }

    private void OnScriptureFinished(Entity<OrbitraRatvarTabletComponent> ent, ref OrbitraRatvarScriptureEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;
        ent.Comp.Busy = false;
        if (!args.Cancelled && SameRitualMind(ent.Comp, args.User)) TryCompleteScripture(ent, args.User, args.Scripture);
        ent.Comp.RitualMind = null;
        UpdateTablet(ent, args.User);
    }

    public bool TryStartScripture(Entity<OrbitraRatvarTabletComponent> tablet, EntityUid user, string id)
    {
        if (tablet.Comp.Busy || !CanRecite(tablet, user, id, out _, out var scripture)) return false;
        var args = new DoAfterArgs(EntityManager, user, scripture.Delay,
            new OrbitraRatvarScriptureEvent { Scripture = id }, tablet, used: tablet)
        {
            BreakOnMove = true, BreakOnDamage = true, BreakOnHandChange = true, NeedHand = true,
        };
        if (!_doAfter.TryStartDoAfter(args)) return false;
        tablet.Comp.Busy = true;
        _mind.TryGetMind(user, out var mind, out _);
        tablet.Comp.RitualMind = mind;
        return true;
    }

    public bool TryCompleteScripture(Entity<OrbitraRatvarTabletComponent> tablet, EntityUid user, string id)
    {
        if (!CanRecite(tablet, user, id, out var rule, out var scripture)) return false;
        if (scripture.Repair)
        {
            if (!TryGetRepairTarget(rule.Owner, user, out var target)) return false;
            rule.Comp.Energy -= scripture.Energy;
            _damage.HealEvenly(target, -scripture.RepairAmount, origin: user);
            return true;
        }
        if (scripture.Result is not { } proto) return false;
        // Повторная проверка и списание в одном серверном вызове исключают двойную покупку.
        rule.Comp.Energy -= scripture.Energy;
        var result = Spawn(proto, Transform(user).Coordinates);
        if (TryComp<OrbitraRatvarShellComponent>(result, out var shell))
        {
            shell.Rule = rule.Owner;
            if (TryComp<GhostRoleComponent>(result, out var ghostRole))
            {
                ghostRole.RoleDescription = "orbitra-ratvar-marauder-description";
                ghostRole.RoleRules = "orbitra-ratvar-marauder-rules";
            }
        }
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"Ratvar cult: {ToPrettyString(user)} recited {id}, spent {scripture.Energy}, created {ToPrettyString(result)}.");
        if (TryComp<OrbitraRatvarStructureComponent>(result, out var structure))
        {
            structure.Rule = rule.Owner;
            var transform = Transform(result);
            // BaseStructure уже закрепляется при спавне; повторное добавление в snap-grid вызывает assert.
            if (scripture.Structure && !transform.Anchored) _transform.AnchorEntity((result, transform));
            if (structure.Ark) rule.Comp.Ark = result;
        }
        return true;
    }

    public bool CanRecite(Entity<OrbitraRatvarTabletComponent> tablet, EntityUid user, string id,
        out Entity<OrbitraRatvarRuleComponent> rule, out OrbitraRatvarScripturePrototype scripture)
    {
        scripture = default!;
        if (!CanUseTablet(tablet, user, out rule) || !tablet.Comp.AllowScriptures || !CanReciteInBody(user) ||
            string.IsNullOrEmpty(id) || !_prototypes.TryIndex<OrbitraRatvarScripturePrototype>(id, out var found) || found.Energy < 0 ||
            found.Tier > GetTier(rule.Comp) || found.Energy > rule.Comp.Energy) return false;
        scripture = found;
        if (found.Repair) return TryGetRepairTarget(rule.Owner, user, out _);
        if (found.Result == "OrbitraRatvarMarauder")
        {
            var living = 0;
            var shells = EntityQueryEnumerator<OrbitraRatvarShellComponent, OrbitraRatvarMarauderComponent>();
            while (shells.MoveNext(out var uid, out var shell, out _))
                if (shell.Rule == rule.Owner && Living(uid)) living++;
            if (living >= rule.Comp.MaxMarauders) return false;
        }
        if (!found.Structure) return true;
        var xform = Transform(user);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var mapGrid) ||
            _map.GetTileRef(grid, mapGrid, xform.Coordinates).Tile.IsEmpty || _containers.IsEntityInContainer(user)) return false;
        var structures = EntityQueryEnumerator<OrbitraRatvarStructureComponent>();
        while (structures.MoveNext(out var uid, out var structure))
        {
            if (Near(uid, user, 0.8f)) return false;
        }
        return found.Result != "OrbitraRatvarArk" || rule.Comp.Ark == null && ValidArkLocation(user, rule.Comp);
    }

    private bool CanUseTablet(Entity<OrbitraRatvarTabletComponent> tablet, EntityUid user,
        out Entity<OrbitraRatvarRuleComponent> rule) =>
        TryGetCult(user, out rule) && Living(user) && _blocker.CanInteract(user, tablet) &&
        (tablet.Comp.RequireHeld ? _hands.IsHolding(user, tablet) :
            _interaction.InRangeUnobstructed(user, tablet.Owner) && Transform(tablet).Anchored);

    private bool TryGetRepairTarget(EntityUid rule, EntityUid user, out EntityUid target)
    {
        target = default;
        var structures = EntityQueryEnumerator<OrbitraRatvarStructureComponent>();
        while (structures.MoveNext(out var uid, out var structure))
        {
            if (structure.Rule != rule || !Near(user, uid, 1.5f) ||
                !_interaction.InRangeUnobstructed(user, uid) || _damage.GetTotalDamage(uid) <= 0) continue;
            target = uid;
            return true;
        }
        return false;
    }

    private void UpdateTablet(Entity<OrbitraRatvarTabletComponent> tablet, EntityUid user)
    {
        if (!CanUseTablet(tablet, user, out var rule))
        {
            _ui.CloseUi(tablet.Owner, OrbitraRatvarUiKey.Key, user);
            return;
        }
        var state = new OrbitraRatvarUiState(rule.Comp.Energy, GetTier(rule.Comp), rule.Comp.Converted.Count, tablet.Comp.Busy);
        foreach (var scripture in _prototypes.EnumeratePrototypes<OrbitraRatvarScripturePrototype>())
        {
            if (!tablet.Comp.AllowScriptures)
                state.Unavailable[scripture.ID] = "orbitra-ratvar-unavailable-obelisk";
            else if (scripture.Tier <= state.Tier && scripture.Energy <= state.Energy &&
                     !CanRecite(tablet, user, scripture.ID, out _, out _))
                state.Unavailable[scripture.ID] = "orbitra-ratvar-unavailable-location";
        }
        _ui.SetUiState(tablet.Owner, OrbitraRatvarUiKey.Key, state);
    }

    private void RefreshTablets()
    {
        var tablets = EntityQueryEnumerator<OrbitraRatvarTabletComponent>();
        while (tablets.MoveNext(out var uid, out var tablet))
            foreach (var actor in _ui.GetActors(uid, OrbitraRatvarUiKey.Key).ToArray())
                UpdateTablet((uid, tablet), actor);
    }
}
