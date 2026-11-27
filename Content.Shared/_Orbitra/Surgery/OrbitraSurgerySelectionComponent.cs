using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Surgery;

/// <summary>Задание конкретного врача. Клиент перехватывает только применение предмета к выбранному пациенту.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraSurgerySelectionComponent : Component
{
    public override bool SendOnlyToOwner => true;
    [AutoNetworkedField] public EntityUid Table;
    [AutoNetworkedField] public EntityUid Patient;
    [AutoNetworkedField] public SurgeryZone Zone;
    [AutoNetworkedField] public SurgeryProcedure Procedure;
    [AutoNetworkedField] public SurgeryOrgan? Organ;

    // Донор или удаляемый орган мог смениться между подтверждением в окне и щелчком.
    public EntityUid? OriginalOrgan;
}

[ByRefEvent]
public readonly record struct OrbitraSurgeryToolUsedEvent(EntityUid Tool);
