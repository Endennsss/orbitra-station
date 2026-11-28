using System.Numerics;
using Content.Shared._Orbitra.Lavaland;

namespace Content.Server._Orbitra.Lavaland;

/// <summary>Единственный маршрут смены. Сторона меняется после физической стыковки.</summary>
[RegisterComponent]
public sealed partial class OrbitraLavalandRouteComponent : Component
{
    public EntityUid StationDock;
    public EntityUid StationTerminal;
    public Vector2 StationTerminalPosition;
    public EntityUid? Ferry;
    public EntityUid? StagingMap;
    public TimeSpan NextStartupDockAttempt;
    public EntityUid? FerryDock;
    public EntityUid? Planet;
    public EntityUid? Outpost;
    public EntityUid? OutpostDock;
    public EntityUid? OutpostTerminal;
    public Vector2 OutpostTerminalPosition;
    public int Seed;
    public LavalandSide Location = LavalandSide.Station;
    public LavalandSide Destination = LavalandSide.Outpost;
    public LavalandFerryStatus Status = LavalandFerryStatus.Preparing;
    public LavalandBlockReason BlockReason;
    public TimeSpan? Deadline;
}

/// <summary>Разрешает маршруту Лаваленда контролировать выход этого борта из БСС.</summary>
[RegisterComponent]
public sealed partial class OrbitraLavalandFerryComponent : Component;

/// <summary>Этот шлюз можно занять вручную, но он зарезервирован при автоматическом подборе причала.</summary>
[RegisterComponent]
public sealed partial class OrbitraLavalandReservedDockComponent : Component;

/// <summary>Сообщает маршруту о потере причала вместе с гридом станции.</summary>
[RegisterComponent]
public sealed partial class OrbitraLavalandStationComponent : Component;
