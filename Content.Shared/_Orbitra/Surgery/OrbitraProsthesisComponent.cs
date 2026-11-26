using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Surgery;

/// <summary>Универсальный комплект, сторону которого определяет хирург при установке.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OrbitraProsthesisComponent : Component
{
    [DataField] public bool Arm;
    [DataField] public bool Advanced;
    [DataField] public Color Finish = Color.FromHex("#798FA6");

    public bool Fits(SurgeryZone zone) => zone switch
    {
        SurgeryZone.LeftArm or SurgeryZone.RightArm => Arm,
        SurgeryZone.LeftLeg or SurgeryZone.RightLeg => !Arm,
        _ => false,
    };
}

/// <summary>Запоминает установленный комплект на проксимальном сегменте протеза.</summary>
[RegisterComponent]
public sealed partial class OrbitraProstheticJointComponent : Component
{
    [DataField] public EntProtoId Kit;
    [DataField] public SurgeryZone Zone;
    [DataField] public bool Advanced;
}

/// <summary>Модели установленных протезов по фактическим местам крепления. Значение true означает улучшенную модель.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraFittedProsthesesComponent : Component
{
    [DataField, AutoNetworkedField] public Dictionary<SurgeryZone, bool> Parts = new();
}
