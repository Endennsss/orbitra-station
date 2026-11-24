using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Particles;

/// <summary>Настройки декоративных частиц. Игровые сущности при выбросе не создаются.</summary>
[Prototype]
public sealed partial class OrbitraParticleEffectPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField] public Color Color = Color.White;
    [DataField] public Color EndColor = Color.Gray;
    [DataField] public float Size = 0.035f;
    [DataField] public float Speed = 1.2f;
    /// <summary>Затухание скорости за секунду.</summary>
    [DataField] public float Drag = 3f;
    /// <summary>Половина ширины области выброса в мировых единицах.</summary>
    [DataField] public float SpawnRadius;
    [DataField] public float Spread = 2.8f;
    [DataField] public float Lifetime = 0.5f;
    [DataField] public float Rate = 8f;
    [DataField] public int Count = 8;
    [DataField] public bool Emissive;
    [DataField] public bool Smoke;
    /// <summary>Начинает движение на расстоянии, с которого частица успеет дойти до источника.</summary>
    [DataField] public bool Inward;
    /// <summary>Временная неподвижная отметина удара из общего бюджета частиц.</summary>
    [DataField] public bool ImpactMark;
    [DataField] public int Shape;
}
