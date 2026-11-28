using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Orbitra.Lavaland;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Roles;
using Content.Shared.Spawners.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Orbitra.Lavaland;

[TestFixture, NonParallelizable]
public sealed class OrbitraLavalandDockTest
{
    private static readonly ProtoId<JobPrototype> Salvager = "SalvageSpecialist";
    private IEntityManager _entities = default!;
    private IPrototypeManager _prototypes = default!;

    [Test]
    public async Task EveryRotationMapCanReceiveTheMiningFerry()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        _entities = server.EntMan;
        _prototypes = server.ProtoMan;
        var errors = new List<string>();
        var loader = _entities.System<MapLoaderSystem>();
        var docks = _entities.System<DockingSystem>();
        var maps = _entities.System<SharedMapSystem>();
        var home = await pair.CreateTestMap();
        ProtoId<GameMapPoolPrototype> defaultPool = "DefaultMapPool";
        ProtoId<GameMapPrototype> dev = "Dev";
        var pool = _prototypes.Index(defaultPool);
        foreach (var id in pool.Maps.Append(dev))
        {
            var station = _prototypes.Index(id);
            var path = station.MapPath;
            TestContext.Out.WriteLine(path);
            try
            {
                await server.WaitAssertion(() =>
                {
                    Assert.That(loader.TryLoadMap(path, out var map, out var grids), Is.True, path.ToString());
                    try
                    {
                        Assert.That(loader.TryLoadGrid(home.MapId, new ResPath("/Maps/_Orbitra/Lavaland/mining.yml"), out var shuttle));
                        var ship = shuttle!.Value.Owner;
                        var shipPort = docks.GetDocks(ship).Single();
                        var dock = grids!.SelectMany(g => docks.GetDocks(g.Owner))
                            .Single(d => _entities.GetComponent<MetaDataComponent>(d).EntityPrototype?.ID == "OrbitraLavalandStationDock");
                        var dockTransform = _entities.GetComponent<TransformComponent>(dock);
                        var grid = dockTransform.GridUid!.Value;
                        if (id != dev)
                            CheckCargoBerths(station, home.MapId, grid, ship, shipPort, dock);

                        var config = docks.GetDockingConfig(ship, grid, shipPort, shipPort.Comp, dock, dock.Comp);
                        Assert.That(config, Is.Not.Null, $"Шаттл пересекает корпус станции на карте {path}.");

                        _entities.System<ShuttleSystem>().FTLDock((ship, _entities.GetComponent<TransformComponent>(ship)), config!);
                        Assert.That(dock.Comp.DockedWith, Is.EqualTo(shipPort.Owner), path.ToString());
                        Assert.That(shipPort.Comp.DockedWith, Is.EqualTo(dock.Owner), path.ToString());
                        docks.Undock(shipPort);
                        Assert.That(dock.Comp.DockedWith, Is.Null, path.ToString());

                        var terminals = _entities.AllEntityQueryEnumerator<OrbitraLavalandTerminalComponent, TransformComponent>();
                        var count = 0;
                        Vector2i terminalTile = default;
                        while (terminals.MoveNext(out var terminal, out var transform))
                        {
                            if (terminal.Side != LavalandSide.Station || transform.GridUid != grid)
                                continue;

                            count++;
                            terminalTile = transform.LocalPosition.Floored();
                            Assert.That(Vector2.Distance(transform.LocalPosition, dockTransform.LocalPosition),
                                Is.LessThanOrEqualTo(4f), path.ToString());
                        }

                        Assert.That(count, Is.EqualTo(1), path.ToString());
                        var entrance = dockTransform.LocalPosition.Floored()
                            + dockTransform.LocalRotation.RotateDir(Direction.North).ToIntVec();
                        CheckBoardingRoute(grid, entrance, terminalTile, id == dev, path);
                    }
                    finally
                    {
                        maps.DeleteMap(map!.Value.Comp.MapId);
                    }
                });
            }
            catch (Exception e)
            {
                TestContext.Out.WriteLine(e);
                errors.Add($"{path} — {e.Message}");
            }
        }

        await pair.CleanReturnAsync();
        Assert.That(errors, Is.Empty);
    }

    private void CheckCargoBerths(GameMapPrototype station, MapId staging, EntityUid grid, EntityUid mining,
        Entity<DockingComponent> ferryPort, Entity<DockingComponent> berth)
    {
        var config = station.Stations.Values.Single();
        var path = config.StationComponentOverrides.TryGetComponent("StationCargoShuttle", out var component)
            ? ((StationCargoShuttleComponent) component).Path
            : new ResPath("/Maps/Shuttles/cargo.yml");
        var loader = _entities.System<MapLoaderSystem>();
        var docks = _entities.System<DockingSystem>();
        var shuttles = _entities.System<ShuttleSystem>();
        Assert.That(loader.TryLoadGrid(staging, path, out var cargo), Is.True, path.ToString());
        var ship = cargo!.Value.Owner;
        var ports = docks.GetDocks(grid).Where(port =>
            _entities.GetComponent<MetaDataComponent>(port).EntityPrototype?.ID == "AirlockExternalGlassShuttleCargo");
        var positions = ports.SelectMany(port => docks.GetDocks(ship).Select(shipPort =>
                docks.GetDockingConfig(ship, grid, shipPort, shipPort.Comp, port, port.Comp)))
            .OfType<DockingConfig>()
            .DistinctBy(candidate => (candidate.Coordinates, candidate.Angle))
            .ToArray();
        Assert.That(positions, Is.Not.Empty, $"На {station.ID} потерян штатный грузовой причал.");

        for (var i = 0; i < positions.Length; i++)
        {
            var position = positions[i];
            shuttles.FTLDock((ship, _entities.GetComponent<TransformComponent>(ship)), position);
            var ferry = docks.GetDockingConfig(mining, grid, ferryPort, ferryPort.Comp, berth, berth.Comp);
            Assert.That(ferry, Is.Not.Null,
                $"На {station.ID} грузовой борт в {position.Coordinates.Position} перекрыл шахтёрский причал.");
            if (i + 1 < positions.Length)
            {
                var (portUid, _, port, _) = position.Docks.Single();
                docks.Undock((portUid, port));
            }
        }
    }

    private static readonly Vector2i[] Steps = [new(0, 1), new(1, 0), new(0, -1), new(-1, 0)];

    private void CheckBoardingRoute(EntityUid grid, Vector2i entrance, Vector2i terminal, bool dev, ResPath path)
    {
        var targets = new HashSet<Vector2i>();
        if (dev)
        {
            var consoles = _entities.AllEntityQueryEnumerator<CargoOrderConsoleComponent, TransformComponent>();
            while (consoles.MoveNext(out _, out var transform))
            {
                if (transform.GridUid == grid)
                    targets.UnionWith(Steps.Select(step => transform.LocalPosition.Floored() + step));
            }
        }
        else
        {
            var spawns = _entities.AllEntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (spawns.MoveNext(out var spawn, out var transform))
            {
                if (transform.GridUid == grid && spawn.Job == Salvager)
                    targets.Add(transform.LocalPosition.Floored());
            }
        }

        var mapGrid = _entities.GetComponent<MapGridComponent>(grid);
        var atmos = _entities.System<AtmosphereSystem>();
        var map = _entities.GetComponent<TransformComponent>(grid).MapUid;
        var maps = _entities.System<SharedMapSystem>();
        var access = _entities.System<AccessReaderSystem>();
        var tags = _prototypes.Index(Salvager).Access.ToArray();
        var visited = new HashSet<Vector2i>();
        var queue = new Queue<(Vector2i From, Vector2i To)>();
        queue.Enqueue((entrance, entrance));
        var canUseTerminal = false;
        var canReachCargo = false;
        while (queue.TryDequeue(out var move))
        {
            var tile = move.To;
            if (visited.Contains(tile) || tile == terminal || maps.GetTileRef(grid, mapGrid, tile).Tile.IsEmpty)
                continue;

            var source = (Vector2) move.From + new Vector2(0.5f);
            var destination = (Vector2) tile + new Vector2(0.5f);
            var sweep = new Box2(Vector2.Min(source, destination), Vector2.Max(source, destination)).Enlarged(0.35f);
            if (Obstructed(move.From, sweep, out _) || Obstructed(tile, sweep, out var door))
                continue;

            if (!door && !(atmos.GetTileMixture(grid, map, tile)?.Pressure > 60f))
                continue;

            visited.Add(tile);
            canUseTerminal |= Steps.Any(delta => tile + delta == terminal);
            canReachCargo |= targets.Contains(tile);
            if (canUseTerminal && canReachCargo)
                return;

            foreach (var delta in Steps)
                queue.Enqueue((tile, tile + delta));
        }

        Assert.Fail($"На {path} нет прохода от {entrance}. Доступ к терминалу {canUseTerminal}, к карго {canReachCargo}.");

        bool Obstructed(Vector2i tile, Box2 sweep, out bool hasDoor)
        {
            hasDoor = false;
            foreach (var uid in maps.GetAnchoredEntities(grid, mapGrid, tile))
            {
                if (_entities.TryGetComponent<DoorComponent>(uid, out var door))
                {
                    if (door.State != DoorState.Open && _entities.TryGetComponent<AccessReaderComponent>(uid, out var reader)
                        && !access.IsAllowed(tags, [], uid, reader))
                    {
                        return true;
                    }

                    hasDoor = true;
                    continue;
                }

                if (!_entities.TryGetComponent<PhysicsComponent>(uid, out var body) || !body.CanCollide
                    || !_entities.TryGetComponent<FixturesComponent>(uid, out var fixtures))
                    continue;

                var transform = _entities.GetComponent<TransformComponent>(uid);
                var pose = new Transform(transform.LocalPosition, transform.LocalRotation);
                foreach (var fixture in fixtures.Fixtures.Values)
                {
                    if (fixture.Hard && (fixture.CollisionLayer & (int) CollisionGroup.MobMask) != 0
                        && fixture.Shape.ComputeAABB(pose, 0).Intersects(sweep))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
