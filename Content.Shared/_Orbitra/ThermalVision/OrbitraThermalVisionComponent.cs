using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.ThermalVision;

/// <summary>Носимый тепловизор с серверной проверкой целей, независимый от обычного зрения.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraThermalVisionComponent : Component
{
    [DataField, AutoNetworkedField] public bool Enabled;
    [AutoNetworkedField] public EntityUid? Wearer;
    [DataField] public EntProtoId Action = "OrbitraActionToggleThermalVision";
    [DataField] public EntityUid? ActionEntity;
}

public sealed partial class OrbitraToggleThermalVisionEvent : InstantActionEvent;

/// <summary>Позволяет специальному снаряжению отозвать допуск тепловых целей без расширения PVS.</summary>
[ByRefEvent]
public record struct OrbitraThermalVisionAttemptEvent(EntityUid User, bool Cancelled = false);
