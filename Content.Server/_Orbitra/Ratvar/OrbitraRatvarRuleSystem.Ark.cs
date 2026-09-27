using Content.Server.RoundEnd;
using Content.Shared.Destructible;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Station;
using Robust.Shared.Map.Components;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Database;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarRuleSystem
{
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private FlammableSystem _fire = default!;

    private static readonly Robust.Shared.Prototypes.EntProtoId ManifestationPrototype = "OrbitraRatvarFinale";

    private void InitializeArk()
    {
        SubscribeLocalEvent<OrbitraRatvarStructureComponent, InteractHandEvent>(OnArkInteract);
        SubscribeLocalEvent<OrbitraRatvarStructureComponent, DestructionEventArgs>(OnStructureDestroyed);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnFinaleRunLevelChanged);
        SubscribeLocalEvent<OrbitraRatvarRuleComponent, ComponentShutdown>(OnFinaleRuleShutdown);
    }

    private void OnFinaleRuleShutdown(Entity<OrbitraRatvarRuleComponent> ent, ref ComponentShutdown args)
    {
        CleanupManifestation(ent.Comp);
    }

    private void OnFinaleRunLevelChanged(GameRunLevelChangedEvent args)
    {
        if (args.New == GameRunLevel.InRound)
            return;
        var query = EntityQueryEnumerator<OrbitraRatvarRuleComponent>();
        while (query.MoveNext(out _, out var rule))
            CleanupManifestation(rule);
    }

    private void OnArkInteract(Entity<OrbitraRatvarStructureComponent> ent, ref InteractHandEvent args)
    {
        if (!args.Handled && ent.Comp.Ark)
            args.Handled = TryActivateArk(ent, args.User);
    }

    private void OnStructureDestroyed(Entity<OrbitraRatvarStructureComponent> ent, ref DestructionEventArgs args)
    {
        if (ent.Comp.Ark && !ent.Comp.DestructionSoundPlayed)
        {
            ent.Comp.DestructionSoundPlayed = true;
            // Звук не привязан к удаляемому ковчегу и работает при любом пороге разрушения.
            _audio.PlayPvs(ent.Comp.DestructionSound, Transform(ent).Coordinates);
        }
        if (ent.Comp.Rule is { } owner && TryComp<OrbitraRatvarRuleComponent>(owner, out var rule) &&
            rule.Ark == ent.Owner && rule.SummonAt != null && !rule.Won)
            LoseArk(rule);
    }

    /// <summary>Starts the irreversible, publicly announced defence phase.</summary>
    public bool TryActivateArk(Entity<OrbitraRatvarStructureComponent> ark, EntityUid user)
    {
        if (!CanActivateArk(ark, user, out var rule)) return false;
        rule.Comp.Energy -= rule.Comp.ArkEnergy;
        rule.Comp.SummonAt = Timing.CurTime + rule.Comp.ArkDefence;
        _appearance.SetData(ark, OrbitraRatvarVisuals.Active, true);
        _audio.PlayPvs(ark.Comp.ActivationSound, ark.Owner);
        _adminLog.Add(LogType.Action, LogImpact.High, $"Ratvar cult: {ToPrettyString(user)} activated {ToPrettyString(ark)}.");
        var position = Transform(ark).LocalPosition;
        _chat.DispatchServerAnnouncement(Loc.GetString("orbitra-ratvar-ark-announcement",
            ("station", Name(rule.Comp.Station!.Value)), ("x", (int) position.X), ("y", (int) position.Y)), Color.Gold);
        return true;
    }

    /// <summary>Checks allegiance, station placement, age, energy and nearby living supporters.</summary>
    public bool CanActivateArk(Entity<OrbitraRatvarStructureComponent> ark, EntityUid user,
        out Entity<OrbitraRatvarRuleComponent> rule)
    {
        rule = default;
        if (TerminatingOrDeleted(ark) || EntityManager.IsQueuedForDeletion(ark) ||
            !TryGetCult(user, out rule) || !ark.Comp.Ark || ark.Comp.Rule != rule.Owner ||
            rule.Comp.Ark != ark.Owner || rule.Comp.SummonAt != null || !Living(user) ||
            !_blocker.CanInteract(user, ark) || !_interaction.InRangeUnobstructed(user, ark.Owner) ||
            !Transform(ark).Anchored || !ValidArkLocation(ark, rule.Comp) ||
            GetTier(rule.Comp) < 3 || Timing.CurTime - rule.Comp.StartedAt < rule.Comp.EarliestArk ||
            rule.Comp.Energy < rule.Comp.ArkEnergy) return false;
        var count = 0;
        foreach (var mind in rule.Comp.Members)
        {
            if (TryComp<MindComponent>(mind, out var data) && data.OwnedEntity is { } body &&
                TryGetCult(body, out var memberRule) && memberRule.Owner == rule.Owner &&
                Living(body) && CanReciteInBody(body) && !_containers.IsEntityInContainer(body) && Near(ark, body, rule.Comp.ArkSupportRange))
                count++;
        }
        return count >= rule.Comp.ArkCultists;
    }

    private bool ValidArkLocation(EntityUid entity, OrbitraRatvarRuleComponent rule)
    {
        var transform = Transform(entity);
        return rule.Station is { } station && _station.GetOwningStation(entity) == station &&
            transform.GridUid is { } grid && _station.GetLargestGrid(station) == grid &&
            TryComp<MapGridComponent>(grid, out var mapGrid) &&
            !_map.GetTileRef(grid, mapGrid, transform.Coordinates).Tile.IsEmpty &&
            !_containers.IsEntityInContainer(entity);
    }

    private void UpdateArk(Entity<OrbitraRatvarRuleComponent> rule)
    {
        if (rule.Comp.Won && rule.Comp.FinalePulseAt is { } pulse && Timing.CurTime >= pulse)
        {
            rule.Comp.FinalePulseAt = null;
            IgniteFinale(rule);
        }
        if (rule.Comp.FinishAt is { } finish && Timing.CurTime >= finish)
        {
            rule.Comp.FinishAt = null;
            CleanupManifestation(rule.Comp);
            _roundEnd.EndRound();
        }
        if (rule.Comp.Won || rule.Comp.Lost || rule.Comp.SummonAt is not { } summon) return;
        if (rule.Comp.Ark is not { } ark || TerminatingOrDeleted(ark) || EntityManager.IsQueuedForDeletion(ark) ||
            !Transform(ark).Anchored || !ValidArkLocation(ark, rule.Comp))
        {
            LoseArk(rule.Comp);
            return;
        }
        if (Timing.CurTime < summon) return;
        rule.Comp.Won = true;
        RefreshCultBodies(rule.Comp);
        rule.Comp.FinishAt = Timing.CurTime + rule.Comp.FinaleDuration;
        rule.Comp.FinalePulseAt = Timing.CurTime + rule.Comp.FinalePulseDelay;
        var god = Spawn(ManifestationPrototype, Transform(ark).Coordinates);
        rule.Comp.Manifestation = god;
        var manifestation = Comp<OrbitraRatvarManifestationComponent>(god);
        manifestation.Rule = rule.Owner;
        manifestation.Grid = Transform(ark).GridUid;
        manifestation.Map = Transform(ark).MapUid;
    }

    private void IgniteFinale(Entity<OrbitraRatvarRuleComponent> rule)
    {
        if (rule.Comp.Station is not { } station || _station.GetLargestGrid(station) is not { } grid ||
            rule.Comp.Manifestation is not { } god || TerminatingOrDeleted(god) || EntityManager.IsQueuedForDeletion(god) ||
            !TryComp<OrbitraRatvarManifestationComponent>(god, out var manifestation) || manifestation.Rule != rule.Owner ||
            manifestation.Grid != grid || Transform(god).GridUid != grid || Transform(god).MapUid != manifestation.Map ||
            _containers.IsEntityInContainer(god))
            return;
        // Единственный обход живых существ за финал, а не за каждый тик или механизм.
        var query = EntityQueryEnumerator<MobStateComponent, FlammableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mob, out var fire, out var transform))
        {
            if (mob.CurrentState == MobState.Dead || transform.GridUid != grid || _containers.IsEntityInContainer(uid))
                continue;
            // TryGetCult отключён после победы: здесь нужна роль разума, а не действующее право читать писания.
            if (_mind.TryGetMind(uid, out var mind, out _) &&
                _roles.MindHasRole<OrbitraRatvarRoleComponent>(mind))
                continue;
            _fire.SetFireStacks(uid, Math.Max(fire.FireStacks, rule.Comp.FinaleFireStacks), fire);
            _fire.Ignite(uid, god, fire);
        }
    }

    private void CleanupManifestation(OrbitraRatvarRuleComponent rule)
    {
        rule.FinalePulseAt = null;
        rule.FinishAt = null;
        if (rule.Manifestation is { } god && !TerminatingOrDeleted(god))
            QueueDel(god);
        rule.Manifestation = null;
    }

    private void LoseArk(OrbitraRatvarRuleComponent rule)
    {
        if (rule.Lost) return;
        rule.Lost = true;
        RefreshCultBodies(rule);
        _chat.DispatchServerAnnouncement(Loc.GetString("orbitra-ratvar-ark-destroyed"), Color.Gold);
    }
}
