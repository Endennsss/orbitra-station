using Content.Shared.Cargo;

namespace Content.Server._Orbitra.Cargo;

/// <summary>
/// Paid deliveries rescued from telepads removed during the communications outage.
/// </summary>
[RegisterComponent]
public sealed partial class OrbitraSuspendedCargoDeliveryComponent : Component
{
    [ViewVariables]
    public List<CargoOrderData> Orders = new();

    [ViewVariables]
    public TimeSpan NextAttempt;
}
