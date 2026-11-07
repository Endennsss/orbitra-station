// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Research.Bluespace;

[Serializable, NetSerializable]
public enum OrbitraBluespaceDeviceUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum OrbitraBluespaceDeviceKind : byte
{
    Harvester,
    Prober,
    Suppressor,
}

[Serializable, NetSerializable]
public enum OrbitraBluespacePowerMode : byte
{
    Normal,
    Overdrive,
}

[Serializable, NetSerializable]
public enum OrbitraBluespaceLootCategory : byte
{
    Materials,
    Supplies,
    Research,
}

[Serializable, NetSerializable]
public enum OrbitraBluespaceDeviceVisuals : byte
{
    Working,
    Unstable,
}

[Serializable, NetSerializable]
public enum OrbitraBluespaceDeviceLayers : byte
{
    Base,
    Status,
}

/// <summary>Reports authoritative operating conditions for a bluespace infrastructure device.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraBluespaceDeviceBoundUserInterfaceState(
    OrbitraBluespaceDeviceKind kind,
    bool enabled,
    bool powered,
    bool anchored,
    bool hasStation,
    bool hasResearchServer,
    bool critical,
    float background,
    float receivedPower,
    float requestedPower,
    float progress,
    float pointsPerMinute,
    OrbitraBluespacePowerMode powerMode,
    OrbitraBluespaceLootCategory lootCategory,
    bool outputBlocked = false,
    float energyPerReward = 0f) : BoundUserInterfaceState
{
    public readonly OrbitraBluespaceDeviceKind Kind = kind;
    public readonly bool Enabled = enabled;
    public readonly bool Powered = powered;
    public readonly bool Anchored = anchored;
    public readonly bool HasStation = hasStation;
    public readonly bool HasResearchServer = hasResearchServer;
    public readonly bool Critical = critical;
    public readonly float Background = background;
    public readonly float ReceivedPower = receivedPower;
    public readonly float RequestedPower = requestedPower;
    public readonly float Progress = progress;
    public readonly float PointsPerMinute = pointsPerMinute;
    public readonly OrbitraBluespacePowerMode PowerMode = powerMode;
    public readonly OrbitraBluespaceLootCategory LootCategory = lootCategory;
    public readonly bool OutputBlocked = outputBlocked;
    public readonly float EnergyPerReward = energyPerReward;
}

/// <summary>Requests an explicit operating state; all machine conditions are checked on the server.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraBluespaceSetEnabledMessage(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}

/// <summary>Selects the harvester's bounded power mode.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraBluespaceSetPowerModeMessage(OrbitraBluespacePowerMode mode) : BoundUserInterfaceMessage
{
    public readonly OrbitraBluespacePowerMode Mode = mode;
}

/// <summary>Selects a configured harvester reward category.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraBluespaceSetLootCategoryMessage(OrbitraBluespaceLootCategory category) : BoundUserInterfaceMessage
{
    public readonly OrbitraBluespaceLootCategory Category = category;
}
