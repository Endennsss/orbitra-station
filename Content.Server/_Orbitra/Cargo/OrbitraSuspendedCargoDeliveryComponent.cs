using Content.Shared.Cargo;

namespace Content.Server._Orbitra.Cargo;

/// <summary>
/// Оплаченные грузы с телепортов, удалённых во время отключения связи.
/// </summary>
[RegisterComponent]
public sealed partial class OrbitraSuspendedCargoDeliveryComponent : Component
{
    [ViewVariables]
    public List<CargoOrderData> Orders = new();

    [ViewVariables]
    public TimeSpan NextAttempt;
}
