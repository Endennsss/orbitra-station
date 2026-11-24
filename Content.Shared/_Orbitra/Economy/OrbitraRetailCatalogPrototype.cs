using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Economy;

/// <summary>Правила розничной цены для торговых автоматов с выбранным ассортиментом.</summary>
[Prototype]
public sealed partial class OrbitraRetailCatalogPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public ProtoId<VendingMachineInventoryPrototype> Inventory;
    [DataField(required: true)] public int DefaultPrice;
    [DataField] public Dictionary<EntProtoId, int> Prices = new();
}
