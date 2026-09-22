using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Particles;

/// <summary>Optional mouth positions in unscaled sprite coordinates for an animal family.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraColdBreathVisualsComponent : Component
{
    [DataField, AutoNetworkedField] public Vector2 South = new(0, -0.12f);
    [DataField, AutoNetworkedField] public Vector2 North = new(0, 0.15f);
    [DataField, AutoNetworkedField] public Vector2 East = new(0.25f, 0);
    [DataField, AutoNetworkedField] public Vector2 West = new(-0.25f, 0);
}
