using Content.Shared.Inventory;
using Content.Shared.Nutrition.Components;

namespace Content.Shared._Orbitra.Particles;

/// <summary>Shared cosmetic eligibility checks; does not alter respiration or equipment.</summary>
public sealed partial class SharedOrbitraColdBreathSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    private EntityQuery<InventoryComponent> _inventoryQuery;
    private EntityQuery<IngestionBlockerComponent> _blockerQuery;

    public override void Initialize()
    {
        _inventoryQuery = GetEntityQuery<InventoryComponent>();
        _blockerQuery = GetEntityQuery<IngestionBlockerComponent>();
    }

    public bool IsMouthCovered(EntityUid uid)
    {
        // Животные без инвентаря тоже дышат; отсутствие слотов не является ошибкой.
        if (!_inventoryQuery.TryComp(uid, out var inventory))
            return false;
        return Blocks(uid, inventory, "head") || Blocks(uid, inventory, "mask");
    }

    private bool Blocks(EntityUid uid, InventoryComponent inventory, string slot) =>
        _inventory.TryGetSlotEntity(uid, slot, out var item, inventory) &&
        _blockerQuery.TryComp(item, out var blocker) && blocker.Enabled;

    public static byte Quantize(float? temperature, float pressure, bool breathing)
    {
        if (!breathing || temperature is not { } kelvin || !float.IsFinite(kelvin) ||
            !float.IsFinite(pressure) || pressure < 20f)
            return 0;
        return (byte) Math.Clamp((int) MathF.Round((278.15f - kelvin) / 20f * 15f), 0, 15);
    }
}
