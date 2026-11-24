using Content.Client.UserInterface.Controls;
using Content.Client.VendingMachines.UI;
using Content.Shared.VendingMachines;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using System.Linq;
using Content.Shared.VendingMachines.Components;
using Content.Shared._Orbitra.Economy; // Orbitra-Edit

namespace Content.Client.VendingMachines;

public sealed class VendingMachineBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private VendingMachineMenu? _menu;

    [ViewVariables]
    private List<VendingMachineInventoryEntry> _cachedInventory = new();

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindowCenteredLeft<VendingMachineMenu>();
        _menu.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        SetOrbitraRetailCatalog(); // Orbitra-Edit
        _menu.OnItemSelected += OnItemSelected;
        Refresh();
    }

    public void Refresh()
    {
        var enabled = EntMan.TryGetComponent(Owner, out VendingMachineEjectComponent? eject) && !eject.Ejecting;

        var system = EntMan.System<VendingMachineSystem>();
        _cachedInventory = system.GetAllInventory(Owner);

        _menu?.Populate(_cachedInventory, enabled);
    }

    public void UpdateAmounts()
    {
        var enabled = EntMan.TryGetComponent(Owner, out VendingMachineEjectComponent? eject) && !eject.Ejecting;

        var system = EntMan.System<VendingMachineSystem>();
        _cachedInventory = system.GetAllInventory(Owner);
        _menu?.UpdateAmounts(_cachedInventory, enabled);
    }

    private void OnItemSelected(GUIBoundKeyEventArgs args, ListData data)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (data is not VendorItemsListData { ItemIndex: var itemIndex })
            return;

        if (_cachedInventory.Count == 0)
            return;

        var selectedItem = _cachedInventory.ElementAtOrDefault(itemIndex);

        if (selectedItem == null)
            return;

        // Orbitra edit start - платную покупку подтверждает сервер.
        var message = new VendingMachineEjectMessage(selectedItem.Type, selectedItem.ID);
        if (GetOrbitraPrice(selectedItem) > 0)
            SendMessage(message);
        else
            SendPredictedMessage(message);
        // Orbitra edit end
    }

    // Orbitra added start - каталог один и тот же на клиенте и сервере.
    private void SetOrbitraRetailCatalog()
    {
        if (_menu is { } menu && EntMan.TryGetComponent(Owner, out VendingMachineComponent? vendor))
            menu.SetRetailCatalog(EntMan.System<OrbitraRetailSystem>(), vendor.PackPrototypeId);
    }

    private int GetOrbitraPrice(VendingMachineInventoryEntry item) =>
        EntMan.TryGetComponent(Owner, out VendingMachineComponent? vendor)
            ? EntMan.System<OrbitraRetailSystem>().GetPrice(vendor.PackPrototypeId, item.ID)
            : 0;
    // Orbitra added end

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (_menu == null)
            return;

        _menu.OnItemSelected -= OnItemSelected;
        _menu.OnClose -= Close;
        _menu.Dispose();
    }
}
