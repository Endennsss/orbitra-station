using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Lavaland;

/// <summary>Заклинивший привод экспедиционного меха остаётся на месте до ремонта проводки.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraSalvageMechComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool DriveRepaired;

    [DataField]
    public TimeSpan RepairDuration = TimeSpan.FromSeconds(5);
}

[Serializable, NetSerializable]
public sealed partial class OrbitraMechDriveRepairEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum OrbitraSalvageMechVisuals : byte
{
    DriveRepaired,
}
