using Content.Server._Orbitra.StationEvents;
using Content.Shared.Shuttles.Components;
using Content.Shared.CCVar;

// ReSharper disable CheckNamespace
namespace Content.Server.Shuttles.Systems;

public sealed partial class ServerArrivalsSystem
{
    [Dependency] private OrbitraAbandonedStationRuleSystem _orbitraAbandonedStation = default!;

    public override bool OrbitraArrivalsSuspended => _orbitraAbandonedStation.IsIsolationActive;

    /// <summary>
    /// Restarts the waiting period instead of executing trips missed during isolation.
    /// </summary>
    public void OrbitraResumeArrivals()
    {
        var next = Timing.CurTime + TimeSpan.FromSeconds(Cfg.GetCVar(CCVars.ArrivalsCooldown));
        var query = EntityQueryEnumerator<ArrivalsShuttleComponent>();
        while (query.MoveNext(out var comp))
        {
            comp.NextTransfer = next;
            comp.NextArrivalsTime = next + TimeSpan.FromSeconds(_shuttles.DefaultTravelTime + _shuttles.DefaultStartupTime);
        }
    }
}
