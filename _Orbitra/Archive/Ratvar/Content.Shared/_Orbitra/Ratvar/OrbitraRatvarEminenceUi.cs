using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

[Serializable, NetSerializable]
public enum OrbitraRatvarEminenceUiKey : byte { Key }

/// <summary>Finite availability reasons; no arbitrary server text is interpreted as markup.</summary>
[Serializable, NetSerializable]
public enum OrbitraRatvarEminenceAvailability : byte
{
    Ready, NoBody, Offline, Unavailable, Container, NotAlive, Blind, OutsideStation,
}

/// <summary>A cult roster entry, not a subscription to the body's entity state.</summary>
[Serializable, NetSerializable]
public readonly record struct OrbitraRatvarEminenceMember(
    NetEntity Mind, string Name, OrbitraRatvarEminenceAvailability Availability);

[Serializable, NetSerializable]
public sealed class OrbitraRatvarEminenceUiState(
    OrbitraRatvarEminenceMember[] members, NetEntity? selected) : BoundUserInterfaceState
{
    public readonly OrbitraRatvarEminenceMember[] Members = members;
    public readonly NetEntity? Selected = selected;
    public int Energy;
    public int RecallCost;
    public int RecallCooldown;
    public bool Recalling;
    public int RealityCost;
    public int RealityCooldown;
    public string RealityReason = "orbitra-ratvar-eminence-ability-select";
    public OrbitraRatvarEminenceDestination[] Destinations = [];
}

/// <summary>Private, cult-owned receiving points and server-generated availability keys.</summary>
[Serializable, NetSerializable]
public readonly record struct OrbitraRatvarEminenceDestination(NetEntity Entity, string Name, string RecallReason, string MassReason);

[Serializable, NetSerializable]
public sealed class OrbitraRatvarEminenceRecallMessage(NetEntity destination) : BoundUserInterfaceMessage
{
    public readonly NetEntity Destination = destination;
}

[Serializable, NetSerializable]
public sealed class OrbitraRatvarEminenceMassRecallMessage(NetEntity destination) : BoundUserInterfaceMessage
{
    public readonly NetEntity Destination = destination;
}

[Serializable, NetSerializable]
public enum OrbitraRatvarEminenceReality : byte { Anomaly, GridCheck }

/// <summary>Finite event choice; arbitrary prototype IDs are never accepted.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraRatvarEminenceRealityMessage(OrbitraRatvarEminenceReality effect) : BoundUserInterfaceMessage
{
    public readonly OrbitraRatvarEminenceReality Effect = effect;
}

/// <summary>Selects a roster mind; the server resolves and validates its current body.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraRatvarEminenceSelectMessage(NetEntity mind) : BoundUserInterfaceMessage
{
    public readonly NetEntity Mind = mind;
}

[Serializable, NetSerializable]
public sealed class OrbitraRatvarEminenceClearMessage : BoundUserInterfaceMessage;

public sealed partial class OrbitraRatvarEminenceMenuEvent : InstantActionEvent;
