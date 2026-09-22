using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Particles;

/// <summary>Data-driven cosmetic particle emission; never creates gameplay entities.</summary>
[Prototype]
public sealed partial class OrbitraParticleEffectPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField] public Color Color = Color.White;
    [DataField] public Color EndColor = Color.Gray;
    [DataField] public float Size = 0.035f;
    [DataField] public float Speed = 1.2f;
    /// <summary>Velocity damping per second.</summary>
    [DataField] public float Drag = 3f;
    /// <summary>Half-width of the emission area in world units.</summary>
    [DataField] public float SpawnRadius;
    [DataField] public float Spread = 2.8f;
    [DataField] public float Lifetime = 0.5f;
    [DataField] public float Rate = 8f;
    [DataField] public int Count = 8;
    [DataField] public bool Emissive;
    [DataField] public bool Smoke;
    /// <summary>Lowest-priority background motes, with a long fade-in.</summary>
    [DataField] public bool Ambient;
    /// <summary>Stationary temporary impact mark, sharing the particle budget.</summary>
    [DataField] public bool ImpactMark;
    [DataField] public int Shape;
}
