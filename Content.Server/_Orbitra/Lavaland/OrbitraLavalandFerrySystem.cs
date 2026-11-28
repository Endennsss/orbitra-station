using System.Diagnostics.CodeAnalysis;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared._Orbitra.Lavaland;
using Content.Shared.GameTicking;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Orbitra.Lavaland;

/// <summary>Оба терминала управляют одним шаттлом и одной планетой до конца смены.</summary>
public sealed partial class OrbitraLavalandFerrySystem : EntitySystem
{
    [Dependency] private ServerStationSystem _station = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private DockingSystem _docks = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private OrbitraLavalandSurfaceSystem _surface = default!;
    [Dependency] private OrbitraLavalandRadioSystem _radio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    private EntityUid? _stationGrid;
    private EntityUid? _routeEntity;
    private static readonly ResPath ShipPath = new("/Maps/_Orbitra/Lavaland/mining.yml");
    private static readonly TimeSpan BoardingTime = TimeSpan.FromSeconds(10);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationPostInitEvent>(OnStationInit, after: [typeof(ShuttleSystem)]);
        SubscribeLocalEvent<OrbitraLavalandFerryComponent, ShuttleFTLLandingEvent>(OnLanding);
        SubscribeLocalEvent<OrbitraLavalandFerryComponent, FTLCompletedEvent>(OnLanded);
        SubscribeLocalEvent<OrbitraLavalandStationComponent, EntityTerminatingEvent>(OnStationTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
    }

    private void OnStationInit(ref StationPostInitEvent ev)
    {
        if (_station.GetLargestGrid(ev.Station.Owner) is {} grid)
            TryInitializeStation(grid);
    }

    public bool TryInitializeStation(EntityUid grid)
    {
        if (_routeEntity != null)
            return grid == _stationGrid;

        if (FindDock(grid, "OrbitraLavalandStationDock") is not {} stationDock ||
            FindTerminal(grid, LavalandSide.Station) is not {} terminal)
            return false;

        // Планета и пассажиры принадлежат смене: разрушение станции не удаляет их.
        var owner = Spawn(null, MapCoordinates.Nullspace);
        var route = EnsureComp<OrbitraLavalandRouteComponent>(owner);
        _routeEntity = owner;
        EnsureComp<OrbitraLavalandStationComponent>(grid);
        route.StationDock = stationDock;
        route.StationTerminal = terminal;
        route.StationTerminalPosition = Transform(terminal).LocalPosition;
        route.Seed = _random.Next();
        _stationGrid = grid;
        SpawnFerry(grid, route);
        return true;
    }

    private void SpawnFerry(EntityUid grid, OrbitraLavalandRouteComponent route)
    {
        // На временной карте борт может дождаться причала, занятого при старте смены.
        route.StagingMap = _maps.CreateMap(out var staging);
        if (!_loader.TryLoadGrid(staging, ShipPath, out var loaded) ||
            FindDock(loaded.Value.Owner, "OrbitraLavalandFerryDock") is not {} port ||
            !TryComp<ShuttleComponent>(loaded.Value.Owner, out var shuttle))
        {
            _maps.DeleteMap(staging);
            route.StagingMap = null;
            route.Status = LavalandFerryStatus.Unavailable;
            return;
        }

        route.Ferry = loaded.Value.Owner;
        route.FerryDock = port;
        EnsureComp<OrbitraLavalandFerryComponent>(loaded.Value.Owner);
        EnsureComp<PreventPilotComponent>(loaded.Value.Owner);
        shuttle.FTLCooldownOverride = TimeSpan.Zero;
        TryDockAtStartup(grid, route);
    }

    private void TryDockAtStartup(EntityUid grid, OrbitraLavalandRouteComponent route)
    {
        route.NextStartupDockAttempt = _timing.CurTime + TimeSpan.FromSeconds(1);
        if (route.Ferry is not {} ship || route.FerryDock is not {} port ||
            GetDockingConfig(ship, port, grid, route.StationDock) is not {} docking)
        {
            SetBlocked(route, PortFailure(route, LavalandSide.Station));
            return;
        }

        _shuttle.FTLDock((ship, Transform(ship)), docking);
        if (!AtPort(route, LavalandSide.Station))
        {
            SetBlocked(route, PortFailure(route, LavalandSide.Station, departure: true));
            return;
        }

        QueueDel(route.StagingMap);
        route.StagingMap = null;
        route.Status = LavalandFerryStatus.Docked;
        route.BlockReason = LavalandBlockReason.None;
    }

    private EntityUid? FindDock(EntityUid grid, string prototype)
    {
        foreach (var dock in _docks.GetDocks(grid))
        {
            if (Transform(dock).GridUid == grid && MetaData(dock).EntityPrototype?.ID == prototype)
                return dock.Owner;
        }
        return null;
    }

    private EntityUid? FindTerminal(EntityUid grid, LavalandSide side)
    {
        var terminals = new HashSet<Entity<OrbitraLavalandTerminalComponent>>();
        _lookup.GetChildEntities(grid, terminals);
        foreach (var terminal in terminals)
        {
            if (terminal.Comp.Side == side && InstalledOn(terminal.Owner, grid))
                return terminal.Owner;
        }
        return null;
    }

    private bool InstalledOn(EntityUid? uid, EntityUid? grid) =>
        grid != null && TryComp<TransformComponent>(uid, out var transform) && transform.Anchored && transform.GridUid == grid;

    public bool TryGetRoute([NotNullWhen(true)] out OrbitraLavalandRouteComponent? route) =>
        TryComp(_routeEntity, out route);

    private bool TryRoute(EntityUid terminal, [NotNullWhen(true)] out OrbitraLavalandRouteComponent? route)
    {
        if (!TryGetRoute(out route))
            return false;

        return terminal == route.StationTerminal && InstalledOn(terminal, _stationGrid) &&
               Transform(terminal).LocalPosition == route.StationTerminalPosition ||
               terminal == route.OutpostTerminal && InstalledOn(terminal, route.Outpost) &&
               Transform(terminal).LocalPosition == route.OutpostTerminalPosition;
    }

    public LavalandConsoleState GetState(EntityUid terminal)
    {
        if (!TryRoute(terminal, out var route))
            return new(LavalandFerryStatus.Unavailable, LavalandSide.Station, LavalandSide.Outpost, null, false);

        var status = route.Status;
        var reason = route.BlockReason;
        if (route.Ferry is {} ship && Deleted(ship))
            status = LavalandFerryStatus.Unavailable;
        else if (status == LavalandFerryStatus.Docked && !AtPort(route, route.Location))
        {
            status = LavalandFerryStatus.Blocked;
            reason = PortFailure(route, route.Location, departure: true);
        }

        if (_stationGrid == null && route.Location == LavalandSide.Outpost && route.Destination == LavalandSide.Station &&
            status is LavalandFerryStatus.Docked or LavalandFerryStatus.Blocked)
            return new(LavalandFerryStatus.Blocked, route.Location, route.Destination, null, false, LavalandBlockReason.MissingDock);

        var canRequest = route.StagingMap == null && (status == LavalandFerryStatus.Docked ||
                         status == LavalandFerryStatus.Blocked && !HasComp<FTLComponent>(route.Ferry));
        var destination = route.StagingMap != null ? LavalandSide.Station : route.Destination;
        var deadline = status is LavalandFerryStatus.Boarding or LavalandFerryStatus.Transit ? route.Deadline : null;
        return new(status, route.Location, destination, deadline, canRequest,
            status == LavalandFerryStatus.Blocked ? reason : LavalandBlockReason.None);
    }

    public void Request(EntityUid terminal, EntityUid user)
    {
        if (!Exists(user) || !TryRoute(terminal, out var route) || !GetState(terminal).CanRequest)
            return;

        if (!LostOrigin(route) && !ConfirmDeparturePort(route))
        {
            SetBlocked(route, PortFailure(route, route.Location, departure: true));
            return;
        }
        if (!EnsureOutpost(route))
        {
            SetBlocked(route, LavalandBlockReason.SurfaceUnavailable);
            return;
        }
        if (DestinationConfig(route) == null)
        {
            SetBlocked(route, PortFailure(route, route.Destination));
            return;
        }

        route.Status = LavalandFerryStatus.Boarding;
        route.Deadline = _timing.CurTime + BoardingTime;
        var warning = route.Location == LavalandSide.Station ? "orbitra-lavaland-warning-station" : "orbitra-lavaland-warning-outpost";
        _radio.Broadcast(terminal, Loc.GetString(warning, ("seconds", (int) BoardingTime.TotalSeconds)));
    }

    private bool EnsureOutpost(OrbitraLavalandRouteComponent route)
    {
        if (route.Planet != null)
            return Exists(route.Planet) && Exists(route.Outpost);

        if (!_surface.TryCreate(route.Seed, out var planet, out var outpost))
            return false;

        if (FindDock(outpost, "OrbitraLavalandOutpostDock") is not {} dock ||
            FindTerminal(outpost, LavalandSide.Outpost) is not {} terminal)
        {
            QueueDel(planet);
            return false;
        }

        route.Planet = planet;
        route.Outpost = outpost;
        route.OutpostDock = dock;
        route.OutpostTerminal = terminal;
        route.OutpostTerminalPosition = Transform(terminal).LocalPosition;
        Comp<OrbitraLavalandTerminalComponent>(terminal).RadioPeer = route.StationTerminal;
        if (TryComp<OrbitraLavalandTerminalComponent>(route.StationTerminal, out var stationTerminal))
            stationTerminal.RadioPeer = terminal;
        return true;
    }

    private static EntityUid? Port(OrbitraLavalandRouteComponent route, LavalandSide side) =>
        side == LavalandSide.Station ? route.StationDock : route.OutpostDock;

    private bool AtPort(OrbitraLavalandRouteComponent route, LavalandSide side)
    {
        var port = Port(route, side);
        var grid = side == LavalandSide.Station ? _stationGrid : route.Outpost;
        return InstalledOn(port, grid) && InstalledOn(route.FerryDock, route.Ferry) &&
               TryComp<DockingComponent>(port, out var ground) && ground.DockedWith == route.FerryDock &&
               TryComp<DockingComponent>(route.FerryDock, out var ship) && ship.DockedWith == port;
    }

    private LavalandBlockReason PortFailure(OrbitraLavalandRouteComponent route, LavalandSide side, bool departure = false)
    {
        var port = Port(route, side);
        var grid = side == LavalandSide.Station ? _stationGrid : route.Outpost;
        if (!InstalledOn(port, grid) || !InstalledOn(route.FerryDock, route.Ferry) ||
            !TryComp<DockingComponent>(port, out var ground) || !HasComp<DockingComponent>(route.FerryDock))
            return LavalandBlockReason.MissingDock;

        if (ground.DockedWith is {} occupant && occupant != route.FerryDock)
            return LavalandBlockReason.Occupied;
        return departure ? LavalandBlockReason.Undocked : LavalandBlockReason.Obstructed;
    }

    private bool LostOrigin(OrbitraLavalandRouteComponent route) =>
        _stationGrid == null && route.Location == LavalandSide.Station && route.Destination == LavalandSide.Outpost;

    private bool ConfirmDeparturePort(OrbitraLavalandRouteComponent route)
    {
        if (AtPort(route, route.Location))
            return true;

        // После ручной отстыковки можно восстановить соединение, пока шлюзы всё ещё совмещены.
        var grid = route.Location == LavalandSide.Station ? _stationGrid : route.Outpost;
        if (route.FerryDock is not {} mobile || Port(route, route.Location) is not {} ground ||
            !InstalledOn(mobile, route.Ferry) || !InstalledOn(ground, grid) ||
            !TryComp<DockingComponent>(mobile, out var a) || !TryComp<DockingComponent>(ground, out var b) ||
            !_docks.CanDock((mobile, a), (ground, b)))
            return false;

        _docks.Dock((mobile, a), (ground, b));
        return AtPort(route, route.Location);
    }

    private DockingConfig? DestinationConfig(OrbitraLavalandRouteComponent route)
    {
        var grid = route.Destination == LavalandSide.Station ? _stationGrid : route.Outpost;
        if (route.Ferry is not {} ship || route.FerryDock is not {} ferryDock || grid is not {} target ||
            Port(route, route.Destination) is not {} port)
            return null;

        return GetDockingConfig(ship, ferryDock, target, port, allowDeparture: true);
    }

    private DockingConfig? GetDockingConfig(EntityUid ship, EntityUid ferryDock, EntityUid grid, EntityUid port, bool allowDeparture = false)
    {
        if (!InstalledOn(ferryDock, ship) || !InstalledOn(port, grid) ||
            !TryComp<DockingComponent>(ferryDock, out var mobile) || !TryComp<DockingComponent>(port, out var ground))
            return null;

        return allowDeparture
            ? _docks.GetDepartureDockingConfig(ship, grid, (ferryDock, mobile), (port, ground))
            : _docks.GetDockingConfig(ship, grid, ferryDock, mobile, port, ground);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!TryGetRoute(out var route))
            return;

        if (route.StagingMap != null && _stationGrid is {} station && !TerminatingOrDeleted(route.Ferry) &&
            _timing.CurTime >= route.NextStartupDockAttempt)
            TryDockAtStartup(station, route);

        if (route.Status != LavalandFerryStatus.Boarding || route.Deadline > _timing.CurTime)
            return;

        if (route.Ferry is not {} ship || !TryComp<ShuttleComponent>(ship, out var shuttle) ||
            !LostOrigin(route) && !AtPort(route, route.Location))
        {
            SetBlocked(route, PortFailure(route, route.Location, departure: true));
            return;
        }
        if (DestinationConfig(route) is not {} config)
        {
            SetBlocked(route, PortFailure(route, route.Destination));
            return;
        }

        var duration = MathF.Max(_shuttle.DefaultTravelTime, _shuttle.DefaultArrivalTime);
        _shuttle.FTLToCoordinates(ship, shuttle, config.Coordinates, config.Angle, startupTime: 0f, hyperspaceTime: duration);
        if (!HasComp<FTLComponent>(ship))
        {
            SetBlocked(route, LavalandBlockReason.Undocked);
            return;
        }
        route.Status = LavalandFerryStatus.Transit;
        route.Deadline = _timing.CurTime + TimeSpan.FromSeconds(duration);
    }

    private void OnLanding(Entity<OrbitraLavalandFerryComponent> ship, ref ShuttleFTLLandingEvent ev)
    {
        if (!TryGetRoute(out var route) || route.Ferry != ship.Owner)
            return;

        ev.Dock = DestinationConfig(route);
        if (ev.Dock != null)
            return;

        ev.Hold = true;
        SetBlocked(route, PortFailure(route, route.Destination));
    }

    private void OnLanded(Entity<OrbitraLavalandFerryComponent> ship, ref FTLCompletedEvent ev)
    {
        if (!TryGetRoute(out var route) || route.Ferry != ship.Owner)
            return;

        if (!AtPort(route, route.Destination))
        {
            SetBlocked(route, PortFailure(route, route.Destination, departure: true));
            return;
        }
        route.Status = LavalandFerryStatus.Docked;
        route.Location = route.Destination;
        route.Destination = route.Location == LavalandSide.Outpost ? LavalandSide.Station : LavalandSide.Outpost;
        route.Deadline = null;
    }

    private void SetBlocked(OrbitraLavalandRouteComponent route, LavalandBlockReason reason)
    {
        if (route.Status != LavalandFerryStatus.Blocked)
        {
            var terminal = TerminatingOrDeleted(route.StationTerminal) ? route.OutpostTerminal : route.StationTerminal;
            if (terminal is {} source)
                _radio.Broadcast(source, Loc.GetString("orbitra-lavaland-blocked"));
        }
        route.Status = LavalandFerryStatus.Blocked;
        route.BlockReason = reason;
        route.Deadline = null;
    }

    private void OnStationTerminating(Entity<OrbitraLavalandStationComponent> ent, ref EntityTerminatingEvent ev)
    {
        if (ent.Owner != _stationGrid || !TryGetRoute(out var route))
            return;

        _stationGrid = null;
        if (route.Destination != LavalandSide.Station)
            return;

        if (route.Status == LavalandFerryStatus.Boarding)
        {
            SetBlocked(route, LavalandBlockReason.MissingDock);
            return;
        }

        if (route.Ferry is not {} ship || !TryComp<FTLComponent>(ship, out var ftl) ||
            ftl.State is not (FTLState.Starting or FTLState.Travelling or FTLState.Arriving) ||
            route.Outpost is not {} outpost || !Exists(outpost))
            return;

        // Земля под станцией исчезла во время рейса: возвращаем пассажиров на тот же аванпост.
        route.Destination = LavalandSide.Outpost;
        var config = DestinationConfig(route);
        ftl.TargetCoordinates = config?.Coordinates ?? new EntityCoordinates(outpost, 0, 0);
        ftl.TargetAngle = config?.Angle ?? Angle.Zero;
        QueueDel(ftl.VisualizerEntity);
        ftl.VisualizerEntity = null;
        Dirty(ship, ftl);
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        if (!TryGetRoute(out var route))
            return;

        QueueDel(route.Ferry);
        QueueDel(route.StagingMap);
        QueueDel(route.Planet);
        QueueDel(_routeEntity);
        _routeEntity = null;
        _stationGrid = null;
    }
}
