using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Particles;

/// <summary>Map-authored, grid-aligned ambient dust region.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraAmbientDustZoneComponent : Component
{
    /// <summary>Width in grid tiles, clamped by the server to 1–32.</summary>
    [DataField, AutoNetworkedField]
    public int Width = 6;

    /// <summary>Height in grid tiles, clamped by the server to 1–32.</summary>
    [DataField, AutoNetworkedField]
    public int Height = 6;
}
