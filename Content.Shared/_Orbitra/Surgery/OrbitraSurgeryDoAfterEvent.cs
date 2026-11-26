using Content.Shared._Orbitra.DoAfter;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Surgery;

[Serializable, NetSerializable]
public sealed partial class OrbitraSurgeryDoAfterEvent : DoAfterEvent, IOrbitraTargetProgress
{
    [DataField]
    public int Ticket;

    public override DoAfterEvent Clone() => new OrbitraSurgeryDoAfterEvent { Ticket = Ticket };
}

[Serializable, NetSerializable]
public sealed class OrbitraSurgeryCancelMessage(NetEntity patient, SurgeryZone zone, SurgeryProcedure procedure,
    int step, SurgeryOrgan? organ = null) : BoundUserInterfaceMessage
{
    public readonly NetEntity Patient = patient;
    public readonly SurgeryZone Zone = zone;
    public readonly SurgeryProcedure Procedure = procedure;
    public readonly int Step = step;
    public readonly SurgeryOrgan? Organ = organ;
}
