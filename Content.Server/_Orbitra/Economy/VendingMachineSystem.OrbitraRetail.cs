using Content.Server._Orbitra.Economy;
using Content.Server.Cargo.Systems;
using Content.Shared.Station.Systems;
using Content.Shared.Cargo.Components;

namespace Content.Server.VendingMachines;

public sealed partial class VendingMachineSystem
{
    [Dependency] private OrbitraBankSystem _orbitraBank = default!;
    [Dependency] private CargoSystem _orbitraCargo = default!;
    [Dependency] private StationSystem _orbitraStation = default!;

    protected override bool TryPayOrbitraRetail(EntityUid vendor, EntityUid actor, int price)
    {
        if (!_orbitraBank.TryPurchase(actor, price))
        {
            Popup.PopupEntity(Loc.GetString("orbitra-retail-payment-denied", ("price", price)), vendor, actor);
            return false;
        }

        if (_orbitraStation.GetOwningStation(vendor) is { } station &&
            TryComp<StationBankAccountComponent>(station, out var bank))
            _orbitraCargo.TryAdjustBankAccount((station, bank), "Service", price);

        return true;
    }
}
