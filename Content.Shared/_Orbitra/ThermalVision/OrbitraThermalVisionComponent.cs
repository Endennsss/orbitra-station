using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.ThermalVision;

/// <summary>Wearable, server-authorized thermal contact scanner. Does not grant normal vision.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraThermalVisionComponent : Component
{
    /// <summary>Maximum detection distance in world units.</summary>
    [DataField, AutoNetworkedField] public float Range = 10f;
    [DataField, AutoNetworkedField] public bool Enabled;
    [AutoNetworkedField] public EntityUid? Wearer;
    [DataField] public EntProtoId Action = "OrbitraActionToggleThermalVision";
    [DataField] public EntityUid? ActionEntity;
}

public sealed partial class OrbitraToggleThermalVisionEvent : InstantActionEvent;
