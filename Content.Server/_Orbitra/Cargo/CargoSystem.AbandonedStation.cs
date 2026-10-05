using Content.Server._Orbitra.StationEvents;
using Content.Server._Orbitra.Cargo;
using Content.Server.Cargo.Components;
using Content.Shared.Cargo.Components;
using Content.Shared.Station.Components;

// ReSharper disable CheckNamespace
namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    [Dependency] private OrbitraAbandonedStationRuleSystem _orbitraAbandonedStation = default!;

    private bool OrbitraTrySuspendDelivery(Entity<CargoTelepadComponent> telepad, EntityUid station)
    {
        if (!_orbitraAbandonedStation.IsIsolationActive)
            return false;

        // Заказы уже оплачены и удалены из обычной базы; повторное одобрение недопустимо.
        var pending = EnsureComp<OrbitraSuspendedCargoDeliveryComponent>(station);
        foreach (var order in telepad.Comp.CurrentOrders)
        {
            if (order.NumDispatched < order.OrderQuantity)
                pending.Orders.Add(order);
        }
        telepad.Comp.CurrentOrders.Clear();
        return true;
    }

    private void OrbitraUpdateSuspendedDeliveries()
    {
        if (_orbitraAbandonedStation.IsIsolationActive)
            return;

        var query = EntityQueryEnumerator<OrbitraSuspendedCargoDeliveryComponent, StationCargoOrderDatabaseComponent, StationDataComponent>();
        while (query.MoveNext(out var uid, out var pending, out var database, out var station))
        {
            if (Timing.CurTime < pending.NextAttempt)
                continue;

            pending.NextAttempt = Timing.CurTime + TimeSpan.FromSeconds(5);
            for (var i = pending.Orders.Count - 1; i >= 0; i--)
            {
                var order = pending.Orders[i];
                if (order.NumDispatched < order.OrderQuantity)
                    TryFulfillOrder((uid, station), order.Account, order, database);

                if (order.NumDispatched >= order.OrderQuantity)
                    pending.Orders.RemoveAt(i);
            }

            if (pending.Orders.Count == 0)
                RemCompDeferred<OrbitraSuspendedCargoDeliveryComponent>(uid);
        }
    }
}
