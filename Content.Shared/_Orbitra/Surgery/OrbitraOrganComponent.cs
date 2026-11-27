using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Surgery;

[Serializable, NetSerializable]
public enum SurgeryOrgan : byte
{
    Heart, Lungs, Liver, Kidneys, Stomach, Eyes, Ears,
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
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraHumanOrganPatientComponent : Component
{
    [AutoNetworkedField] public SurgeryOrganCondition Eyes;
    /// <summary>Невылеченный проникающий урон от пуль с последней травмы органа и порог следующей травмы.</summary>
    [DataField] public FixedPoint2 BallisticDamage;
    [DataField] public FixedPoint2 BallisticOrganThreshold;
    [DataField, AutoNetworkedField] public float DamagedHeartRecovery = 0.45f;
    [DataField, AutoNetworkedField] public float FailedHeartBloodLoss = 2f;
    [DataField, AutoNetworkedField] public float DamagedBreathFraction = 0.8f;
    [DataField, AutoNetworkedField] public float DamagedLiverInterval = 2f;
}
