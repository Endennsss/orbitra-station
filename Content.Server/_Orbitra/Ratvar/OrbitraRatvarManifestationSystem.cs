using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using System.Numerics;
using Content.Server.Singularity.EntitySystems;
using Content.Server.Wires;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Content.Shared.Access.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Lock;
using Content.Shared.Maps;
using Content.Shared.Station;
using Content.Shared.Tiles;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Bounded, station-local adaptation of Bee's roaming Ratvar and ratvar_act territory conversion.</summary>
public sealed partial class OrbitraRatvarManifestationSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private ConstructionSystem _construction = default!;
    [Dependency] private TileSystem _tiles = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private GravityWellSystem _gravityWell = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPvsOverrideSystem _pvsOverride = default!;

    private static readonly Vector2i[] Directions = [new(1, 0), new(0, 1), new(-1, 0), new(0, -1)];
    private readonly List<EntityUid> _anchored = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarManifestationComponent, GetVerbsEvent<Verb>>(OnVerbs);
        SubscribeLocalEvent<OrbitraRatvarManifestationComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<OrbitraRatvarManifestationComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OrbitraRatvarManifestationComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<OrbitraRatvarManifestationComponent> ent, ref ComponentStartup args)
    {
        _pvsOverride.AddGlobalOverride(ent);
    }

    private void OnShutdown(Entity<OrbitraRatvarManifestationComponent> ent, ref ComponentShutdown args)
    {
        _pvsOverride.RemoveGlobalOverride(ent);
        ent.Comp.MusicStream = _audio.Stop(ent.Comp.MusicStream);
    }

    private void OnVerbs(Entity<OrbitraRatvarManifestationComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor) || !_admin.HasAdminFlag(actor.PlayerSession, AdminFlags.Fun) || ent.Comp.Rule != null)
            return;
        var session = actor.PlayerSession;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(ent.Comp.PreviewUntil > _timing.CurTime ? "orbitra-ratvar-preview-stop" : "orbitra-ratvar-preview-start"),
            Message = Loc.GetString("orbitra-ratvar-preview-warning"),
            Category = VerbCategory.Debug,
            Impact = LogImpact.High,
            Act = () => TryTogglePreview(ent, session),
        });
    }

    private void OnExamine(Entity<OrbitraRatvarManifestationComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Rule == null)
            args.PushMarkup(Loc.GetString(ent.Comp.PreviewUntil > _timing.CurTime ? "orbitra-ratvar-preview-active" : "orbitra-ratvar-preview-inactive"));
    }

    /// <summary>Explicitly starts or stops a bounded destructive admin test without creating a cult or ending the round.</summary>
    public bool TryTogglePreview(Entity<OrbitraRatvarManifestationComponent> god, ICommonSession admin)
    {
        if (!_admin.HasAdminFlag(admin, AdminFlags.Fun) || TerminatingOrDeleted(god) || god.Comp.Rule != null)
            return false;
        if (god.Comp.PreviewUntil > _timing.CurTime)
        {
            god.Comp.PreviewUntil = null;
            _adminLog.Add(LogType.Action, LogImpact.High, $"{admin.Name} stopped Ratvar preview {ToPrettyString(god)}.");
            return true;
        }
        var xform = Transform(god);
        if (xform.GridUid is not { } grid || xform.MapUid == null || !HasComp<MapGridComponent>(grid) || _containers.IsEntityInContainer(god))
            return false;
        god.Comp.Grid = grid;
        god.Comp.Map = xform.MapUid;
        god.Comp.PreviewUntil = _timing.CurTime + TimeSpan.FromSeconds(60);
        god.Comp.NextMove = _timing.CurTime;
        god.Comp.NextUpdate = _timing.CurTime;
        _adminLog.Add(LogType.Action, LogImpact.High, $"{admin.Name} started destructive Ratvar preview {ToPrettyString(god)} on {ToPrettyString(grid)} for 60 seconds.");
        return true;
    }

    /// <summary>Processes a bounded portion of the god's radius without deleting arbitrary entities.</summary>
    public bool TryTransformTerritory(Entity<OrbitraRatvarManifestationComponent> god)
    {
        if (!CanAct(god, out var grid) || _timing.CurTime < god.Comp.NextUpdate)
            return false;
        god.Comp.NextUpdate = _timing.CurTime + god.Comp.Interval;
        if (!_prototypes.TryIndex(god.Comp.RecipeSource, out var recipeSource) ||
            !recipeSource.Components.TryGetValue("OrbitraRatvarFabricator", out var entry) ||
            entry.Component is not OrbitraRatvarFabricatorComponent recipes)
            return false;
        var center = _transform.GetGridTilePositionOrDefault(god.Owner, grid.Comp);
        var radius = Math.Clamp(god.Comp.Radius, 0, 12);
        var width = radius * 2 + 1;
        var area = width * width;
        for (var n = 0; n < Math.Clamp(god.Comp.TileBudget, 1, 32); n++)
        {
            var cursor = god.Comp.TileCursor++ % area;
            var offset = new Vector2i(cursor % width - radius, cursor / width - radius);
            if (offset.X * offset.X + offset.Y * offset.Y > radius * radius)
                continue;
            var indices = center + offset;
            var tile = _map.GetTileRef(grid, grid.Comp, indices);
            if (tile.Tile.IsEmpty)
                continue;
            if (recipes.Floors.TryGetValue(new ProtoId<ContentTileDefinition>(_tileDefinitions[tile.Tile.TypeId].ID), out var output))
                _tiles.ReplaceTile(tile, _prototypes.Index(output));

            // Снимок клетки: ChangeGraph меняет индекс закреплённых сущностей во время преобразования.
            _anchored.Clear();
            foreach (var entity in _map.GetAnchoredEntities(grid, grid.Comp, indices))
                _anchored.Add(entity);
            foreach (var entity in _anchored)
                TryTransformStructure(god, entity, recipes);
        }
        return true;
    }

    /// <summary>Only the registered manifestation of an active, victorious rule can alter its station.</summary>
    public bool CanAct(Entity<OrbitraRatvarManifestationComponent> god, out Entity<MapGridComponent> grid)
    {
        grid = default;
        if (god.Comp.Rule == null && god.Comp.PreviewUntil > _timing.CurTime &&
            !TerminatingOrDeleted(god) && !EntityManager.IsQueuedForDeletion(god) &&
            god.Comp.Grid is { } previewGrid && TryComp<MapGridComponent>(previewGrid, out var previewMapGrid) &&
            !TerminatingOrDeleted(previewGrid) && !EntityManager.IsQueuedForDeletion(previewGrid) &&
            god.Comp.Map is { } previewMap && !TerminatingOrDeleted(previewMap) && !EntityManager.IsQueuedForDeletion(previewMap) &&
            Transform(god).GridUid == previewGrid && Transform(god).MapUid == previewMap && !_containers.IsEntityInContainer(god))
        {
            grid = (previewGrid, previewMapGrid);
            return true;
        }
        if (TerminatingOrDeleted(god) || EntityManager.IsQueuedForDeletion(god) ||
            god.Comp.Rule is not { } owner || !TryComp<OrbitraRatvarRuleComponent>(owner, out var rule) ||
            !rule.Won || rule.Lost || rule.Manifestation != god.Owner ||
            (!_ticker.IsGameRuleActive(owner) && _ticker.RunLevel != GameRunLevel.PostRound) ||
            god.Comp.Grid is not { } gridId || TerminatingOrDeleted(gridId) || EntityManager.IsQueuedForDeletion(gridId) ||
            god.Comp.Map is not { } mapId || TerminatingOrDeleted(mapId) || EntityManager.IsQueuedForDeletion(mapId) ||
            !TryComp<MapGridComponent>(gridId, out var mapGrid) ||
            rule.Station is not { } station || _station.GetLargestGrid(station) != gridId ||
            Transform(god).GridUid != gridId || Transform(god).MapUid != god.Comp.Map ||
            _containers.IsEntityInContainer(god))
            return false;
        grid = (gridId, mapGrid);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<OrbitraRatvarManifestationComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextUpdate && _timing.CurTime < comp.NextMove)
                continue;
            if (!CanAct((uid, comp), out var grid))
            {
                comp.MusicStream = _audio.Stop(comp.MusicStream);
                continue;
            }
            if (comp.MusicStream == null)
            {
                var music = _audio.PlayPvs(comp.Music, uid, AudioParams.Default.WithLoop(true).WithVolume(-12));
                comp.MusicStream = music?.Entity;
                if (music != null) _audio.SetMapAudio(music);
            }
            TryTransformTerritory((uid, comp));
            if (_timing.CurTime < comp.NextMove)
                continue;
            comp.NextMove = _timing.CurTime + comp.MoveInterval;
            if (_timing.CurTime >= comp.NextPull)
            {
                comp.NextPull = _timing.CurTime + TimeSpan.FromSeconds(1);
                _gravityWell.GravPulse(uid, Math.Clamp(comp.PullRange, 0, 10), 1,
                    baseRadialDeltaV: Math.Clamp(comp.PullVelocity, 0, 6));
            }
            if (_timing.CurTime >= comp.NextTurn || comp.Heading == Vector2.Zero)
            {
                var angle = _random.NextFloat() * MathF.Tau;
                comp.Heading = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                comp.NextTurn = _timing.CurTime + TimeSpan.FromSeconds(3);
            }
            var position = Transform(uid).LocalPosition;
            var distance = Math.Clamp(comp.MoveSpeed, 0, 4) * (float) Math.Clamp(comp.MoveInterval.TotalSeconds, 0.01, 0.25);
            var destination = position + comp.Heading * distance;
            if (!grid.Comp.LocalAABB.Contains(destination))
            {
                comp.Heading = -comp.Heading;
                destination = position + comp.Heading * distance;
            }
            if (grid.Comp.LocalAABB.Contains(destination))
                _transform.SetCoordinates(uid, new EntityCoordinates(grid, destination));
        }
    }

    private bool TryTransformStructure(EntityUid god, EntityUid target, OrbitraRatvarFabricatorComponent recipes)
    {
        if (!CanTransformStructure(target, recipes, out var kind))
            return false;
        if (kind == ConversionKind.Airlock)
            _construction.AddContainer(target, "board");
        return kind switch
        {
            ConversionKind.Wall => _construction.ChangeNode(target, god, "brassWall", performActions: false),
            ConversionKind.Window => _construction.ChangeGraph(target, god, "OrbitraRatvarWindow", "window", performActions: false),
            _ => _construction.ChangeGraph(target, god, "OrbitraRatvarDoor", "door", performActions: false),
        };
    }

    private bool CanTransformStructure(EntityUid target, OrbitraRatvarFabricatorComponent recipes, out ConversionKind kind)
    {
        kind = default;
        if (TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target) ||
            !TryComp<ConstructionComponent>(target, out var construction) || construction.TargetNode != null ||
            construction.InteractionQueue.Count != 0 || _containers.IsEntityInContainer(target) ||
            Prototype(target) is not { } prototype)
            return false;
        var id = new EntProtoId(prototype.ID);
        if (recipes.Airlocks.TryGetValue(id, out var airlockNode))
        {
            if (construction.Graph.Id != "Airlock" || construction.Node != airlockNode ||
                !TryComp<DoorComponent>(target, out var door) || door.State != DoorState.Closed ||
                !HasComp<AirlockComponent>(target) || HasComp<LockComponent>(target) ||
                TryComp<DoorBoltComponent>(target, out var bolts) && bolts.BoltsDown ||
                !_containers.TryGetContainer(target, "board", out var board) ||
                Transform(target).ChildCount != board.ContainedEntities.Count)
                return false;
            foreach (var container in _containers.GetAllContainers(target))
            {
                if (container.ID != "board")
                    return false;
            }
            kind = ConversionKind.Airlock;
            return true;
        }
        if (HasComp<ContainerManagerComponent>(target) || Transform(target).ChildCount != 0 ||
            HasComp<AccessReaderComponent>(target) || HasComp<WiresComponent>(target) || HasComp<LockComponent>(target))
            return false;
        if (recipes.Walls.TryGetValue(id, out var wallNode) && construction.Graph.Id == "Girder" && construction.Node == wallNode)
        {
            kind = ConversionKind.Wall;
            return true;
        }
        if (recipes.Windows.TryGetValue(id, out var windowNode) && construction.Graph.Id == "Window" && construction.Node == windowNode)
        {
            kind = ConversionKind.Window;
            return true;
        }
        if (recipes.Doors.TryGetValue(id, out var doorNode) && construction.Graph.Id == "DoorGraph" && construction.Node == doorNode &&
            TryComp<DoorComponent>(target, out var materialDoor) && materialDoor.State == DoorState.Closed)
        {
            kind = ConversionKind.Door;
            return true;
        }
        return false;
    }

    private enum ConversionKind : byte { Wall, Window, Door, Airlock }
}
