namespace Content.Server._Orbitra.Ratvar;

/// <summary>Cult-owned installation; ownership survives a builder changing bodies.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarStructureComponent : Component
{
    public EntityUid? Rule;
    [DataField] public bool ConversionSigil;
    [DataField] public bool Ark;
    [DataField] public bool Slowing;
    [DataField] public float SlowMultiplier = 0.5f;
    [DataField] public TimeSpan SlowDuration = TimeSpan.FromSeconds(3);
}
