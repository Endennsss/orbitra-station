using Content.Server._Orbitra.StationEvents;
using Content.Shared.Fax.Components;

// ReSharper disable CheckNamespace
namespace Content.Server.Fax;

public sealed partial class FaxSystem
{
    [Dependency] private OrbitraAbandonedStationRuleSystem _orbitraAbandonedStation = default!;

    /// <summary>
    /// Refreshes discovery and open windows after a change in external connectivity.
    /// </summary>
    public void OrbitraRefreshFaxes()
    {
        var query = EntityQueryEnumerator<FaxMachineComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            Refresh(uid, comp);
            UpdateUserInterface(uid, comp);
        }
    }

    private Dictionary<string, string> OrbitraGetKnownFaxes(EntityUid uid, FaxMachineComponent comp)
    {
        if (!_orbitraAbandonedStation.IsIsolationActive)
            return comp.KnownFaxes;

        var known = new Dictionary<string, string>();
        foreach (var (address, name) in comp.KnownFaxes)
        {
            if (_orbitraAbandonedStation.CanFax(uid, address))
                known.Add(address, name);
        }
        return known;
    }
}
