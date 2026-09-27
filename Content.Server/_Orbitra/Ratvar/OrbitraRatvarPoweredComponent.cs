namespace Content.Server._Orbitra.Ratvar;

/// <summary>Local coverage and shared energy requirements for a cult mechanism.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarPoweredComponent : Component
{
    /// <summary>Cost per operation, or per projectile for weapons.</summary>
    [DataField] public int EnergyPerUse = 5;

    /// <summary>Minimum reserve required even when the operation itself is free.</summary>
    [DataField] public int MinimumEnergy = 1;
}
