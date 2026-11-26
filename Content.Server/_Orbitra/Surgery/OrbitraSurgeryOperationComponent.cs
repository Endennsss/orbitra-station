using Content.Shared._Orbitra.Surgery;
using Content.Shared.DoAfter;

namespace Content.Server._Orbitra.Surgery;

/// <summary>Завершённые шаги остаются у пациента при смене стола или хирурга.</summary>
[RegisterComponent]
public sealed partial class OrbitraSurgeryOperationComponent : Component
{
    [DataField]
    public SurgeryZone Zone;

    [DataField]
    public SurgeryProcedure Procedure;

    [DataField]
    public int Step;

    [DataField]
    public SurgeryOrgan? Organ;

    /// <summary>Фиксирует конкретный орган для восстановления или извлечения.</summary>
    [DataField]
    public EntityUid? OriginalOrgan;

    /// <summary>Блокировка действует только в течение текущего действия.</summary>
    public EntityUid? Surgeon;
    public EntityUid? Table;
    public int Ticket;
    public DoAfterId? Pending;
    public bool Finishing;
    public TimeSpan StartedAt;
    public TimeSpan Duration;
}
