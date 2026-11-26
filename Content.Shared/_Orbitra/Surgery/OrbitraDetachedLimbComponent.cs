using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Surgery;

/// <summary>Привязывает отделённые органы к исходному пациенту, стороне и месту крепления.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraDetachedLimbComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid OriginalOwner;

    [DataField, AutoNetworkedField]
    public SurgeryZone Zone;

    [DataField]
    public EntityUid OriginalParent;

    [DataField]
    public EntityUid Root;

    [DataField]
    public EntityUid Extremity;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraMissingLegsComponent : Component
{
    [AutoNetworkedField]
    public int Count;
}

/// <summary>Тяжёлый удар запрашивает штатное отделение одной конкретной конечности.</summary>
[ByRefEvent]
public record struct OrbitraDismemberAttemptEvent(SurgeryZone Zone)
{
    public bool Handled;
}
