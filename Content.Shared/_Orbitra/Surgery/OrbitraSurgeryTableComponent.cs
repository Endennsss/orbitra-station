using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class OrbitraSurgeryTableComponent : Component
{
    [DataField] public bool FieldTable;
    [DataField] public float StepDurationMultiplier = 1f;

    public static bool Supports(bool fieldTable, SurgeryProcedure procedure) =>
        !fieldTable || procedure is SurgeryProcedure.InternalBleeding or SurgeryProcedure.Shrapnel
            or SurgeryProcedure.OpenAccess or SurgeryProcedure.CloseAccess or SurgeryProcedure.DressAccess;
}
