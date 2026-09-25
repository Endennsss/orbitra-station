using Content.Server.Antag.Selectors;
using Robust.Shared.Random;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Two founders below twenty players, three from twenty onward.</summary>
public sealed partial class OrbitraRatvarAntagCount : AntagCountSelector
{
    [DataField] public int LargerCrewThreshold = 20;
    [DataField] public int SmallCrewCount = 2;
    [DataField] public int LargeCrewCount = 3;
    public override int GetTargetAntagCount(IRobustRandom random, int playerCount) =>
        playerCount < LargerCrewThreshold ? SmallCrewCount : LargeCrewCount;
}
