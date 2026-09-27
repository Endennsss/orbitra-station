using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

[Serializable, NetSerializable]
public enum OrbitraRatvarTravelUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class OrbitraRatvarTravelMessage(NetEntity destination) : BoundUserInterfaceMessage
{
    public readonly NetEntity Destination = destination;
}

[Serializable, NetSerializable]
public sealed class OrbitraRatvarTravelNameMessage(string name) : BoundUserInterfaceMessage
{
    public readonly string Name = name;
}

[Serializable, NetSerializable]
public sealed class OrbitraRatvarTravelUiState(string name, int energy, int cost, string reason,
    Dictionary<NetEntity, string> destinations) : BoundUserInterfaceState
{
    public readonly string Name = name;
    public readonly int Energy = energy;
    public readonly int Cost = cost;
    public readonly string Reason = reason;
    public readonly Dictionary<NetEntity, string> Destinations = destinations;
    /// <summary>Disabled destinations remain visible with a server-validated failure reason.</summary>
    public Dictionary<NetEntity, string> Unavailable = [];
}

[Serializable, NetSerializable]
public sealed partial class OrbitraRatvarTravelEvent : SimpleDoAfterEvent
{
    public NetEntity Rule;
    public NetEntity Mind;
    public NetEntity Destination;
}
