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

[Serializable, NetSerializable]
public enum SurgeryAccess : byte
{
    Closed, Incised, Clamped, Open,
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
    RepairOrgan = 10, ExtractOrgan = 11, TransplantOrgan = 12,
    InstallProsthesis = 20, RemoveProsthesis = 21,
    OpenAccess = 30, CloseAccess, DressAccess,
}

/// <summary>Общее состояние стола. Выбор области до начала операции у каждого врача свой.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraSurgeryState(NetEntity? patient, string patientName,
    Dictionary<SurgeryZone, SurgeryInjury> injuries, SurgeryZone? activeZone,
    SurgeryProcedure? activeProcedure, int step, bool busy, TimeSpan? startedAt = null, TimeSpan duration = default,
    Dictionary<SurgeryOrgan, SurgeryOrganCondition>? organs = null, SurgeryOrgan? activeOrgan = null) : BoundUserInterfaceState
{
    public Dictionary<SurgeryZone, bool> Prostheses = new();
    public readonly NetEntity? Patient = patient;
    public readonly Dictionary<SurgeryOrgan, SurgeryOrganCondition> Organs = organs ?? new();
    public readonly SurgeryOrgan? ActiveOrgan = activeOrgan;
    public Dictionary<SurgeryZone, SurgeryAccess> Access = new();
    public HashSet<SurgeryZone> Dressed = new();
    public SurgeryAccess GetAccess(SurgeryZone zone) => Access.GetValueOrDefault(zone);
    public int GetStep(SurgeryProcedure procedure, SurgeryZone zone) =>
        ActiveZone == zone && ActiveProcedure == procedure ? Step :
        procedure == SurgeryProcedure.OpenAccess ? (int) GetAccess(zone) : 0;
    public bool CanAccess(SurgeryProcedure procedure, SurgeryZone zone) =>
        OrbitraSurgerySteps.Allows(procedure, GetAccess(zone), Dressed.Contains(zone)) &&
        (!FieldTable || procedure != SurgeryProcedure.OpenAccess || GetAccess(zone) != SurgeryAccess.Closed ||
         (Injuries.GetValueOrDefault(zone) & (SurgeryInjury.Shrapnel | SurgeryInjury.InternalBleeding)) != 0);
    public bool HasFindings(SurgeryZone zone) => Injuries.GetValueOrDefault(zone) != SurgeryInjury.None ||
        GetAccess(zone) != SurgeryAccess.Closed || OrbitraOrganSystem.HasFindings(Organs, zone);
    public readonly string PatientName = patientName;
    public bool AcceptsProstheses { get; init; }
    public bool FieldTable { get; init; }
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
    SurgeryProcedure procedure, int step, bool confirmed, SurgeryOrgan? organ = null) : BoundUserInterfaceMessage
{
    public readonly NetEntity Patient = patient;
    public readonly SurgeryZone Zone = zone;
    public readonly SurgeryProcedure Procedure = procedure;
    public readonly int Step = step;
    public readonly SurgeryOrgan? Organ = organ;
    public readonly bool Confirmed = confirmed;
}
