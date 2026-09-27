using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Player;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarRuleSystem
{
    [Dependency] private IAdminManager _admin = default!;

    /// <summary>Configures only the invoking administrator's cult. Disabling preserves earned progression.</summary>
    public bool TryConfigureTestMode(ICommonSession admin, int? tier, int? energy)
    {
        if (!CanConfigureTestMode(admin, tier, energy, out var rule))
            return false;
        rule.Comp.TestTier = tier;
        if (tier != null && energy is { } stock)
            rule.Comp.Energy = stock;
        _adminLog.Add(LogType.AdminMessage, LogImpact.High,
            $"Ratvar test mode: {admin.Name} configured {ToPrettyString(rule)}: tier={tier}, energy={rule.Comp.Energy}.");
        RefreshTablets();
        return true;
    }

    /// <summary>Rejects unauthorized sessions, ended cults and invalid configuration without mutations.</summary>
    public bool CanConfigureTestMode(ICommonSession admin, int? tier, int? energy,
        out Entity<OrbitraRatvarRuleComponent> rule)
    {
        rule = default;
        return _admin.HasAdminFlag(admin, AdminFlags.Fun) &&
            admin.AttachedEntity is { } body && TryGetCult(body, out rule) &&
            (tier == null || tier is >= 1 and <= 3) &&
            (energy == null || tier != null && energy >= 0 && energy <= rule.Comp.MaxEnergy);
    }
}
