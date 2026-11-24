using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Economy;

/// <summary>Разрешает розничные цены заранее и обновляет их при перезагрузке прототипов.</summary>
public sealed partial class OrbitraRetailSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly Dictionary<ProtoId<VendingMachineInventoryPrototype>, OrbitraRetailCatalogPrototype> _catalogs = new();
    private readonly Dictionary<EntProtoId, int> _exportLimits = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        _catalogs.Clear();
        _exportLimits.Clear();
        _loaded = false;
    }

    private bool _loaded;

    private void EnsureCatalogs()
    {
        if (_loaded)
            return;

        foreach (var catalog in _prototype.EnumeratePrototypes<OrbitraRetailCatalogPrototype>())
        {
            _catalogs.Add(catalog.Inventory, catalog);
            var inventory = _prototype.Index(catalog.Inventory);
            AddExportLimits(inventory.StartingInventory, catalog);
            AddExportLimits(inventory.ContrabandInventory, catalog);
            AddExportLimits(inventory.EmaggedInventory, catalog);
        }

        _loaded = true;
    }

    private void AddExportLimits(Dictionary<EntProtoId, uint>? inventory, OrbitraRetailCatalogPrototype catalog)
    {
        if (inventory == null)
            return;

        foreach (var item in inventory.Keys)
        {
            var price = catalog.Prices.GetValueOrDefault(item, catalog.DefaultPrice);
            // Бесплатный товар тоже может быть выставлен на экспорт, поэтому ограничиваем и его.
            var limit = Math.Max(0, price / 2);
            _exportLimits[item] = Math.Min(limit, _exportLimits.GetValueOrDefault(item, limit));
        }
    }

    /// <summary>Возвращает ноль для бесплатного ассортимента и служебных автоматов.</summary>
    public int GetPrice(ProtoId<VendingMachineInventoryPrototype> inventory, EntProtoId item)
    {
        EnsureCatalogs();
        return _catalogs.TryGetValue(inventory, out var catalog)
            ? Math.Max(0, catalog.Prices.GetValueOrDefault(item, catalog.DefaultPrice))
            : 0;
    }

    /// <summary>Ограничивает экспортную стоимость самого розничного товара.</summary>
    public double LimitExportPrice(EntProtoId item, double price)
    {
        EnsureCatalogs();
        return _exportLimits.TryGetValue(item, out var limit) ? Math.Min(price, limit) : price;
    }
}
