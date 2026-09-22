using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Particles;

/// <summary>Derived grid-local regions, available independently of marker PVS membership.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraAmbientDustGridComponent : Component
{
    /// <summary>Runtime-only regions; reconstructed from saved markers, never map-serialized.</summary>
    [AutoNetworkedField]
    public List<Box2> Regions = new();
}
