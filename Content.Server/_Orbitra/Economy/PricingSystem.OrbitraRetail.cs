using Content.Shared._Orbitra.Economy;
using Robust.Shared.Prototypes;

namespace Content.Server.Cargo.Systems;

public sealed partial class PricingSystem
{
    [Dependency] private OrbitraRetailSystem _orbitraRetail = default!;

    private double LimitOrbitraRetailExport(EntityPrototype prototype, double price) =>
        _orbitraRetail.LimitExportPrice(prototype.ID, price);
}
