using Content.Server.RoundEnd;
using Content.Shared.Destructible;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Station;
using Robust.Shared.Map.Components;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Database;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarRuleSystem
{
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    private void InitializeArk()
    {
        SubscribeLocalEvent<OrbitraRatvarStructureComponent, InteractHandEvent>(OnArkInteract);
        SubscribeLocalEvent<OrbitraRatvarStructureComponent, DestructionEventArgs>(OnStructureDestroyed);
    }

    private void OnArkInteract(Entity<OrbitraRatvarStructureComponent> ent, ref InteractHandEvent args)
    {
        if (!args.Handled && ent.Comp.Ark)
            args.Handled = TryActivateArk(ent, args.User);
    }

    private void OnStructureDestroyed(Entity<OrbitraRatvarStructureComponent> ent, ref DestructionEventArgs args)
    {
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
        if (!TryGetCult(user, out rule) || !ark.Comp.Ark || ark.Comp.Rule != rule.Owner ||
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
        if (rule.Comp.FinishAt is { } finish && Timing.CurTime >= finish)
        {
            rule.Comp.FinishAt = null;
            _roundEnd.EndRound();
        }
        if (rule.Comp.Won || rule.Comp.Lost || rule.Comp.SummonAt is not { } summon) return;
        if (rule.Comp.Ark is not { } ark || Deleted(ark) || !Transform(ark).Anchored || !ValidArkLocation(ark, rule.Comp))
        {
            LoseArk(rule.Comp);
            return;
        }
        if (Timing.CurTime < summon) return;
        rule.Comp.Won = true;
        rule.Comp.FinishAt = Timing.CurTime + TimeSpan.FromSeconds(8);
        Spawn("OrbitraRatvarFinale", Transform(ark).Coordinates);
    }

    private void LoseArk(OrbitraRatvarRuleComponent rule)
    {
        if (rule.Lost) return;
        rule.Lost = true;
        _chat.DispatchServerAnnouncement(Loc.GetString("orbitra-ratvar-ark-destroyed"), Color.Gold);
    }
}
