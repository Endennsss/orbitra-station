using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Tiles;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarFabricatorSystem
{
    // Преобразование пола не трогает сущности на клетке и использует штатную историю покрытий.
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private FloorTileSystem _floorTile = default!;
    [Dependency] private ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private void InitializeFloors()
    {
        SubscribeLocalEvent<OrbitraRatvarFabricatorComponent, OrbitraRatvarFloorEvent>(OnFloorFinished);
        SubscribeLocalEvent<OrbitraRatvarFabricatorComponent, DoAfterAttemptEvent<OrbitraRatvarFloorEvent>>(OnFloorAttempt);
    }

    private void OnFloorAttempt(Entity<OrbitraRatvarFabricatorComponent> ent, ref DoAfterAttemptEvent<OrbitraRatvarFloorEvent> args)
    {
        if (!CanFabricateFloor(ent, args.DoAfter.Args.User, (OrbitraRatvarFloorEvent) args.DoAfter.Args.Event,
                out _, out _, out _))
            args.Cancel();
    }

    private void OnFloorFinished(Entity<OrbitraRatvarFabricatorComponent> ent, ref OrbitraRatvarFloorEvent args)
    {
        if (args.Handled || ent.Comp.Pending != args.DoAfter.Id)
            return;
        args.Handled = true;
        ent.Comp.Pending = null;
        if (!args.Cancelled)
            TryFinishFloor(ent, args.User, args);
    }

    /// <summary>Starts fabrication of one explicitly supported floor without charging for failed attempts.</summary>
    public bool TryStartFloor(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, EntityCoordinates coordinates)
    {
        if (tool.Comp.Pending != null || !_transform.IsValid(coordinates) ||
            !_cult.TryGetCult(user, out var rule) || !_mind.TryGetMind(user, out var mind, out _) ||
            Transform(user).GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp) ||
            _transform.GetGrid(coordinates) != grid || Transform(grid).MapUid is not { } map)
            return false;

        var tile = _map.GetTileRef(grid, gridComp, coordinates);
        var context = new OrbitraRatvarFloorEvent
        {
            Rule = GetNetEntity(rule.Owner), Mind = GetNetEntity(mind), Grid = GetNetEntity(grid),
            Map = GetNetEntity(map), Indices = tile.GridIndices, SourceTile = tile.Tile.TypeId,
        };
        if (!CanFabricateFloor(tool, user, context, out _, out _, out _))
            return false;

        // У клетки нет сущности-цели: удалённый грид проверяет наш Attempt, не DoAfter.Target.
        var operation = new DoAfterArgs(EntityManager, user, tool.Comp.FloorDelay, context, tool, used: tool)
        {
            BreakOnMove = true, BreakOnDamage = true, BreakOnHandChange = true, NeedHand = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };
        if (!_doAfter.TryStartDoAfter(operation, out var id))
            return false;
        tool.Comp.Pending = id;
        return true;
    }

    private bool TryFinishFloor(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user, OrbitraRatvarFloorEvent context)
    {
        if (!CanFabricateFloor(tool, user, context, out var rule, out var tile, out var output))
            return false;
        // Резервируем стоимость до событий замены тайла; отказ штатной системы возвращает резерв.
        rule.Comp.Energy -= tool.Comp.FloorEnergy;
        if (!_tile.ReplaceTile(tile, output))
        {
            rule.Comp.Energy += tool.Comp.FloorEnergy;
            return false;
        }
        _adminLog.Add(LogType.Tile, LogImpact.Low,
            $"Ratvar cult: {ToPrettyString(user)} fabricated {output.ID} at {ToPrettyString(tile.GridUid)} {tile.GridIndices}, spent {tool.Comp.FloorEnergy} from {ToPrettyString(rule)}.");
        _popup.PopupEntity(Loc.GetString("orbitra-ratvar-fabricator-floor-done"), tool, user);
        return true;
    }

    private bool CanFabricateFloor(Entity<OrbitraRatvarFabricatorComponent> tool, EntityUid user,
        OrbitraRatvarFloorEvent context, out Entity<OrbitraRatvarRuleComponent> rule,
        out TileRef tile, out ContentTileDefinition output)
    {
        rule = default;
        tile = default;
        output = default!;
        var grid = GetEntity(context.Grid);
        if (TerminatingOrDeleted(tool) || TerminatingOrDeleted(user) || TerminatingOrDeleted(grid) ||
            tool.Comp.FloorEnergy < 0 || tool.Comp.FloorDelay <= TimeSpan.Zero ||
            !_cult.TryGetCult(user, out rule) || rule.Owner != GetEntity(context.Rule) ||
            rule.Comp.Energy < tool.Comp.FloorEnergy || !_mind.TryGetMind(user, out var mind, out _) ||
            mind != GetEntity(context.Mind) || !TryComp<MobStateComponent>(user, out var mob) || mob.CurrentState != MobState.Alive ||
            !_hands.IsHolding(user, tool) || !_blocker.CanInteract(user, null) || _container.IsEntityInContainer(user) ||
            !TryComp<MapGridComponent>(grid, out var gridComp) || Transform(user).GridUid != grid ||
            Transform(grid).MapUid != GetEntity(context.Map))
            return false;

        tile = _map.GetTileRef(grid, gridComp, context.Indices);
        if (tile.Tile.IsEmpty || tile.Tile.TypeId != context.SourceTile ||
            !tool.Comp.Floors.TryGetValue(new ProtoId<ContentTileDefinition>(_tileDefinition[tile.Tile.TypeId].ID), out var result) ||
            !_prototype.TryIndex(result, out var definition) || definition.TileId == tile.Tile.TypeId ||
            !_floorTile.CanPlaceTile(grid, gridComp, context.Indices, out _))
            return false;

        output = definition;
        var center = _map.GridTileToLocal(grid, gridComp, context.Indices);
        return _interaction.InRangeUnobstructed(user, _transform.ToMapCoordinates(center));
    }
}
