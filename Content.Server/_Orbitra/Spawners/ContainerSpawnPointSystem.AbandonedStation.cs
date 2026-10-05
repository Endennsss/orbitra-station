using Content.Server._Orbitra.StationEvents;

// ReSharper disable CheckNamespace
namespace Content.Server.Spawners.EntitySystems;

public sealed partial class ContainerSpawnPointSystem
{
    [Dependency] private OrbitraAbandonedStationRuleSystem _orbitraAbandonedStation = default!;
}
