// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Server._Orbitra.Research.Bluespace;

/// <summary>Хранит общий блюспейс-фон и пороги блокировки на сущности станции.</summary>
[RegisterComponent]
[Access(typeof(OrbitraBluespaceBackgroundSystem), typeof(OrbitraBluespaceResearchSystem))]
public sealed partial class OrbitraBluespaceBackgroundComponent : Component
{
    /// <summary>Сколько фона исчезает за секунду без паузы и работы устройств.</summary>
    [DataField]
    public float DecayPerSecond = 0.03f;

    /// <summary>Первый порог предупреждения станции по шкале от 0 до 100.</summary>
    [DataField]
    public float WarningThreshold = 60f;

    /// <summary>Общий порог для вторжений и автоматического синего кода.</summary>
    [DataField]
    public float IncursionThreshold = 70f;

    /// <summary>Порог предупреждения о сильной нестабильности.</summary>
    [DataField]
    public float DangerThreshold = 85f;

    /// <summary>Уровень фона, при котором опасные устройства станции блокируются.</summary>
    [DataField]
    public float CriticalThreshold = 95f;

    /// <summary>Для разблокировки станции фон должен стать строго ниже этого уровня.</summary>
    [DataField]
    public float RecoveryThreshold = 80f;

    /// <summary>Задержка повторного предупреждения того же уровня. Усиление опасности предупреждается сразу.</summary>
    [DataField]
    public TimeSpan WarningCooldown = TimeSpan.FromSeconds(60);

    [ViewVariables(VVAccess.ReadOnly)]
    internal float Level;

    internal bool Critical;
    internal bool SecurityAlertHandled;
    internal float WarningCooldownRemaining;
    internal int LastWarningBand;
    internal int ActiveProbers;
}
