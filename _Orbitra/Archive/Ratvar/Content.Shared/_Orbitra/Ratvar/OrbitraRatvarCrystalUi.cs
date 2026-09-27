using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

[Serializable, NetSerializable]
public enum OrbitraRatvarCrystalUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class OrbitraRatvarCrystalProjectMessage(NetEntity destination) : BoundUserInterfaceMessage
{
    public readonly NetEntity Destination = destination;
}

[Serializable, NetSerializable]
public sealed class OrbitraRatvarCrystalRenameMessage(string name) : BoundUserInterfaceMessage
{
    public readonly string Name = name;
}

/// <summary>Only a cult's own crystal labels and readiness, never remote entity state.</summary>
[Serializable, NetSerializable]
public readonly record struct OrbitraRatvarCrystalDestination(NetEntity Entity, string Name, int X, int Y, string Reason);

[Serializable, NetSerializable]
public sealed class OrbitraRatvarCrystalUiState(string name, OrbitraRatvarCrystalDestination[] destinations) : BoundUserInterfaceState
{
    public readonly string Name = name;
    public readonly OrbitraRatvarCrystalDestination[] Destinations = destinations;
}
