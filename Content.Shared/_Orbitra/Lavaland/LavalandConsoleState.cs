using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Lavaland;

[Serializable, NetSerializable]
public enum LavalandSide : byte
{
    Station,
    Outpost,
}

[Serializable, NetSerializable]
public enum LavalandFerryStatus : byte
{
    Preparing,
    Docked,
    Boarding,
    Transit,
    Blocked,
    Unavailable,
}

[Serializable, NetSerializable]
public enum LavalandBlockReason : byte
{
    None,
    Occupied,
    Obstructed,
    MissingDock,
    Undocked,
    SurfaceUnavailable,
}

[Serializable, NetSerializable]
public enum OrbitraLavalandUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class OrbitraLavalandRequestMessage : BoundUserInterfaceMessage;

/// <summary>Абсолютный игровой срок позволяет клиенту отсчитывать секунды между сменами состояния рейса.</summary>
[Serializable, NetSerializable]
public sealed class LavalandConsoleState(LavalandFerryStatus status, LavalandSide location,
    LavalandSide destination, TimeSpan? deadline, bool canRequest,
    LavalandBlockReason blockReason = LavalandBlockReason.None) : BoundUserInterfaceState
{
    public readonly LavalandFerryStatus Status = status;
    public readonly LavalandSide Location = location;
    public readonly LavalandSide Destination = destination;
    public readonly TimeSpan? Deadline = deadline;
    public readonly bool CanRequest = canRequest;
    public readonly LavalandBlockReason BlockReason = blockReason;

    public override bool Equals(object? obj) => obj is LavalandConsoleState other &&
        Status == other.Status && Location == other.Location && Destination == other.Destination &&
        Deadline == other.Deadline && CanRequest == other.CanRequest && BlockReason == other.BlockReason;

    public override int GetHashCode() => HashCode.Combine(Status, Location, Destination, Deadline, CanRequest, BlockReason);
}
