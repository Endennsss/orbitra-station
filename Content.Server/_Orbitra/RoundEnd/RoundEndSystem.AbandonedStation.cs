using Content.Server._Orbitra.StationEvents;

// ReSharper disable CheckNamespace
namespace Content.Server.RoundEnd;

public sealed partial class RoundEndSystem
{
    [Dependency] private OrbitraAbandonedStationRuleSystem _orbitraAbandonedStation = default!;
}
