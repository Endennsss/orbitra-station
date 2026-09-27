using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>Decorative connection within normal PVS, with no physics or remote viewing authority.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraRatvarProjectionVisualComponent : Component
{
    /// <summary>Receiving crystal. Missing or detached endpoints must not be rendered.</summary>
    [AutoNetworkedField] public EntityUid? Anchor;

    /// <summary>Brass tint of the connection.</summary>
    [DataField, AutoNetworkedField] public Color Color = new(0.78f, 0.64f, 0.37f, 0.65f);
}
