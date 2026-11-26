using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Surgery;

/// <summary>Пороги относятся к одному удару после применения защиты.</summary>
[Prototype]
public sealed partial class OrbitraTraumaPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField(required: true)] public OrbitraTraumaRoll Fracture = new();
    [DataField(required: true)] public OrbitraTraumaRoll InternalBleeding = new();
    [DataField(required: true)] public OrbitraTraumaRoll Shrapnel = new();
    [DataField(required: true)] public OrbitraTraumaRoll Dismemberment = new();
    [DataField(required: true)] public HashSet<EntProtoId> FragmentProjectiles = new();

    [DataField] public OrbitraTraumaRoll OrganInjury = new();
    [DataField] public OrbitraTraumaRoll OrganFailure = new();

    [DataField] public float FracturedLegSpeed = 0.72f;
    [DataField] public float SplintedLegSpeed = 0.91f;
    [DataField] public float FracturedArmDelay = 1.6f;
    [DataField] public float SplintedArmDelay = 1.15f;
    [DataField] public float TickSeconds = 3f;
    [DataField] public float DismemberBloodLoss = 12f;
    [DataField] public float DismemberBleedRate = 2.5f;
    [DataField] public FixedPoint2 BloodLoss = 3f;
    [DataField] public FixedPoint2 BandagedBloodLoss = 1f;
    [DataField] public DamageSpecifier FragmentDamage = new();
    [DataField] public float DressingSeconds = 120f;
    [DataField] public float SplintSeconds = 300f;
}

[DataDefinition]
public sealed partial class OrbitraTraumaRoll
{
    [DataField(required: true)] public FixedPoint2 Threshold;
    [DataField(required: true)] public float Chance;
}
