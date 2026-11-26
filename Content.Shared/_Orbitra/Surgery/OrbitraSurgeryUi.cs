using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Surgery;

[Serializable, NetSerializable]
public enum OrbitraSurgeryUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum SurgeryZone : byte
{
    Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg,
}

[Flags, Serializable, NetSerializable]
public enum SurgeryInjury : byte
{
    None = 0,
    Fracture = 1,
    InternalBleeding = 2,
    Shrapnel = 4,
    Missing = 8,
    Splinted = 16,
    Bandaged = 32,
}

[Serializable, NetSerializable]
public enum SurgeryProcedure : byte
{
    Fracture, InternalBleeding, Shrapnel, Amputate, Reattach,
}

/// <summary>Общее состояние стола. Выбор области до начала операции у каждого врача свой.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraSurgeryState(NetEntity? patient, string patientName,
    Dictionary<SurgeryZone, SurgeryInjury> injuries, SurgeryZone? activeZone,
    SurgeryProcedure? activeProcedure, int step, bool busy, TimeSpan? startedAt = null, TimeSpan duration = default) : BoundUserInterfaceState
{
    public readonly NetEntity? Patient = patient;
    public readonly string PatientName = patientName;
    public readonly Dictionary<SurgeryZone, SurgeryInjury> Injuries = injuries;
    public readonly SurgeryZone? ActiveZone = activeZone;
    public readonly SurgeryProcedure? ActiveProcedure = activeProcedure;
    public readonly int Step = step;
    public readonly bool Busy = busy;
    public readonly TimeSpan? StartedAt = startedAt;
    public readonly TimeSpan Duration = duration;
}

[Serializable, NetSerializable]
public sealed class OrbitraSurgeryStepMessage(NetEntity patient, SurgeryZone zone,
    SurgeryProcedure procedure, int step, bool confirmed) : BoundUserInterfaceMessage
{
    public readonly NetEntity Patient = patient;
    public readonly SurgeryZone Zone = zone;
    public readonly SurgeryProcedure Procedure = procedure;
    public readonly int Step = step;
    public readonly bool Confirmed = confirmed;
}
