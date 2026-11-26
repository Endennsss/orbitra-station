using Content.Shared.Mobs;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Surgery;

/// <summary>Личные показания открытых операционных столов получает только владелец анализатора.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraSurgeryMonitorComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [AutoNetworkedField]
    public Dictionary<NetEntity, OrbitraSurgeryReading> Readings = new();
}

[Serializable, NetSerializable]
public readonly record struct OrbitraSurgeryReading(
    NetEntity Patient, MobState State, float? Temperature, float? BloodLevel, OrbitraBloodTrend Trend);

[Serializable, NetSerializable]
public enum OrbitraBloodTrend : byte
{
    Unknown, Stable, Falling, Rising,
}
