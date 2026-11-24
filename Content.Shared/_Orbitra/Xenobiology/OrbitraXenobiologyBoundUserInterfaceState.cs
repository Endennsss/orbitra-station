// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Xenobiology;

[Serializable, NetSerializable]
public enum OrbitraXenobiologyUiKey : byte
{
    Key,
}

/// <summary>Доступные для просмотра измерения с запитанной консоли колонии.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraXenobiologyBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool Powered;
    public float Range;
    public List<OrbitraXenobiologySpecimen> Specimens = new();
}

/// <summary>Серверное состояние входа, режима и прогресса обработки в процессоре слизней.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraXenobiologyProcessorBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool Powered;
    public bool HasInput;
    public bool Processing;
    public bool CanProcess;
    public bool CanEject;
    public float Progress;
    public float SecondsRemaining;
    public float Duration;
    public int ExpectedYield;
    public OrbitraXenobiologySpecimen? Input;
    public OrbitraXenobiologyProcessorStatus Status;
}

[Serializable, NetSerializable]
public enum OrbitraXenobiologyProcessorStatus : byte
{
    NoPower,
    Empty,
    Ready,
    Processing,
    InvalidInput,
}

/// <summary>Измеренный образец. Лабораторные действия используют идентификаторы с сервера.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraXenobiologySpecimen
{
    public NetEntity Entity;
    public string Name = string.Empty;
    public string Identifier = string.Empty;
    public string Breed = string.Empty;
    public bool Adult;
    public bool Dead;
    public bool Hostile;
    public bool Suspended;
    public int Generation;
    public float Growth;
    public float Biomass;
    public float Hunger;
    public float Temperature;
    public float MinimumTemperature;
    public float MaximumTemperature;
    public float GrowthBiomass;
    public float DivisionBiomass;
    public float MutationChance;
    public int Offspring;
    public int ExtractYield;
    public float GrowthRate;
    public float Docility;
    public float ThermalTolerance;
}

/// <summary>Запрашивает обработку загруженного образца с повторной проверкой на сервере.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraXenobiologyProcessMessage : BoundUserInterfaceMessage;

/// <summary>Запрашивает безопасную выдачу загруженного образца.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraXenobiologyEjectMessage : BoundUserInterfaceMessage;
