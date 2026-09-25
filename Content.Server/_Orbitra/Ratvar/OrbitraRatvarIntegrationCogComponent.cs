using Content.Shared.DoAfter;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Installation timings and conversion of APC charge to shared cult energy.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarIntegrationCogComponent : Component
{
    /// <summary>Time required to open a closed APC.</summary>
    [DataField] public TimeSpan OpenDelay = TimeSpan.FromSeconds(5);
    /// <summary>Time required to insert the cog into an open APC.</summary>
    [DataField] public TimeSpan InstallDelay = TimeSpan.FromSeconds(4);
    /// <summary>Base prying time to destroy an installed cog.</summary>
    [DataField] public TimeSpan RemoveDelay = TimeSpan.FromSeconds(5);
    /// <summary>Interval between extraction attempts.</summary>
    [DataField] public TimeSpan Interval = TimeSpan.FromSeconds(2);
    /// <summary>Maximum cult energy extracted per interval.</summary>
    [DataField] public int EnergyPerInterval = 20;
    /// <summary>Explicit Orbitra adaptation from cult energy to SS14 joules.</summary>
    [DataField] public float JoulesPerEnergy = 100;
    /// <summary>Minimum battery fraction permitting extraction.</summary>
    [DataField] public float MinimumChargeFraction = 0.5f;
    /// <summary>Charge left untouched, expressed in cult energy units.</summary>
    [DataField] public int ReserveEnergy = 20;
}

/// <summary>APC-local ownership; the dedicated container stores the actual cog.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class OrbitraRatvarInstalledCogComponent : Component
{
    /// <summary>Cult that installed this cog.</summary>
    public EntityUid Rule;
    /// <summary>Earliest next extraction attempt.</summary>
    [AutoPausedField] public TimeSpan NextExtraction;
}

/// <summary>APC-local operations that must be cancelled before the target disappears.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarCogOperationsComponent : Component
{
    /// <summary>Active installation and removal operations targeting this APC.</summary>
    public readonly List<DoAfterId> Pending = [];
}
