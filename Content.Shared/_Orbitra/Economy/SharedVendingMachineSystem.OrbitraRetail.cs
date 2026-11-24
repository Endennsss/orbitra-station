using Content.Shared._Orbitra.Economy;
using Content.Shared.VendingMachines.Components;

namespace Content.Shared.VendingMachines;

public abstract partial class SharedVendingMachineSystem
{
    [Dependency] private OrbitraRetailSystem _orbitraRetail = default!;

    private bool TryCollectOrbitraPayment(EntityUid vendor, EntityUid? actor, VendingMachineComponent machine,
        VendingMachineInventoryEntry entry)
    {
        if (actor == null || actor == vendor)
            return true;

        var price = _orbitraRetail.GetPrice(machine.PackPrototypeId, entry.ID);
        return price == 0 || TryPayOrbitraRetail(vendor, actor.Value, price);
    }

    // Баланс доступен только на сервере; платная выдача начинается после подтверждения.
    protected virtual bool TryPayOrbitraRetail(EntityUid vendor, EntityUid actor, int price) => false;
}
