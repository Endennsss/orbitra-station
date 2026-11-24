using Content.Shared._Orbitra.Economy;
using Content.Shared.VendingMachines;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.VendingMachines.UI;

public sealed partial class VendingMachineMenu
{
    private OrbitraRetailSystem? _orbitraRetail;
    private ProtoId<VendingMachineInventoryPrototype> _orbitraCatalog;

    internal void SetRetailCatalog(OrbitraRetailSystem retail, ProtoId<VendingMachineInventoryPrototype> catalog)
    {
        _orbitraRetail = retail;
        _orbitraCatalog = catalog;
    }

    private void AddOrbitraPrice(VendingMachineItem row, EntProtoId item)
    {
        var price = _orbitraRetail?.GetPrice(_orbitraCatalog, item) ?? 0;
        if (price == 0)
            return;

        row.AddChild(new Label
        {
            Text = Loc.GetString("orbitra-retail-vending-price", ("price", price)),
            Margin = new Thickness(4, 0, 8, 0)
        });
    }
}
