using System.Text;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Forensics.Components;

public sealed partial class ForensicScannerComponent
{
    // Для осколка неизвестного происхождения сохраняется пустая строка. Для остальных типов образцов сохраняется null.
    [DataField, AutoNetworkedField] public string? OrbitraProjectile;

    public void AppendOrbitraFragment(StringBuilder text, IPrototypeManager prototypes)
    {
        if (OrbitraProjectile == null)
            return;
        var name = OrbitraProjectile.Length != 0 && prototypes.TryIndex<EntityPrototype>(OrbitraProjectile, out var projectile)
            ? projectile.Name
            : Loc.GetString("orbitra-forensic-ammo-unknown");
        text.AppendLine(Loc.GetString("orbitra-forensic-ammo", ("type", name)));
        text.AppendLine();
    }
}
