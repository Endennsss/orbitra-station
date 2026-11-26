using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Surgery;

[Serializable, NetSerializable]
public enum SurgeryOrgan : byte
{
    Heart, Lungs, Liver,
}

[Serializable, NetSerializable]
public enum SurgeryOrganCondition : byte
{
    Healthy, Damaged, Nonfunctional, Missing,
}

/// <summary>Состояние принадлежит самому органу и сохраняется после извлечения и пересадки.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraOrganComponent : Component
{
    [DataField(required: true)] public SurgeryOrgan Kind;
    [DataField, AutoNetworkedField] public SurgeryOrganCondition Condition;
}

/// <summary>Анатомия человека, поддерживающая операции на внутренних органах.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OrbitraHumanOrganPatientComponent : Component
{
    [DataField] public float DamagedHeartRecovery = 0.45f;
    [DataField] public float FailedHeartBloodLoss = 2f;
    [DataField] public float DamagedBreathFraction = 0.8f;
    [DataField] public float DamagedLiverInterval = 2f;
}
