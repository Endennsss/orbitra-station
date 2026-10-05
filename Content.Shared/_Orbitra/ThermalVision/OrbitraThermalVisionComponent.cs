using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.ThermalVision;

/// <summary>Wearable, server-authorized thermal contact scanner. Does not grant normal vision.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraThermalVisionComponent : Component
{
    [DataField, AutoNetworkedField] public bool Enabled;
    [AutoNetworkedField] public EntityUid? Wearer;
    [DataField] public EntProtoId Action = "OrbitraActionToggleThermalVision";
    [DataField] public EntityUid? ActionEntity;
}

public sealed partial class OrbitraToggleThermalVisionEvent : InstantActionEvent;

/// <summary>Allows specialized equipment to revoke thermal authorization without expanding PVS.</summary>
[ByRefEvent]
public record struct OrbitraThermalVisionAttemptEvent(EntityUid User, bool Cancelled = false);
