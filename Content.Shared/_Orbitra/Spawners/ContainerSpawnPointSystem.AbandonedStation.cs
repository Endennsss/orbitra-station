using Content.Shared.Shuttles.Systems;

// ReSharper disable CheckNamespace
namespace Content.Shared.Spawners.EntitySystems;

public sealed partial class ContainerSpawnPointSystem
{
    [Dependency] private ArrivalsSystem _orbitraArrivals = default!;
}
