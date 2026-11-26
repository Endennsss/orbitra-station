using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Surgery;

/// <summary>Органический гуманоид с настройками травматологии.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OrbitraSurgicalPatientComponent : Component
{
    [DataField] public ProtoId<OrbitraTraumaPrototype> TraumaProfile = "OrbitraHumanoidTrauma";
}

/// <summary>Присутствует только у пациентов с активной локальной травмой.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraTraumaComponent : Component
{
    [AutoNetworkedField] public Dictionary<SurgeryZone, SurgeryInjury> Injuries = new();

    // Тип снаряда хранится на пациенте до извлечения, даже если сам снаряд давно удалён.
    [DataField] public Dictionary<SurgeryZone, EntProtoId> FragmentSources = new();

    // Остаток времени считается только пока карта активна.
    public Dictionary<SurgeryZone, float> StabilizationRemaining = new();
    public float Elapsed;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class OrbitraTraumaDressingComponent : Component
{
    [DataField(required: true)] public SurgeryInjury Treatment;
    [DataField] public TimeSpan Delay = TimeSpan.FromSeconds(3);
}

[Serializable, NetSerializable]
public sealed partial class OrbitraTraumaDressingDoAfterEvent : DoAfterEvent
{
    [DataField] public SurgeryZone Zone;

    public override DoAfterEvent Clone() => new OrbitraTraumaDressingDoAfterEvent { Zone = Zone };
}

[ByRefEvent]
public record struct OrbitraHandActionDelayEvent(float Multiplier);
