// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared._Orbitra.Research.Bluespace;
using Content.Shared.Database;

namespace Content.Server._Orbitra.Research.Bluespace;

public sealed partial class OrbitraBluespaceHarvesterSystem
{
    // Orbitra-Edit: управление принимает только доступные актору сообщения открытого интерфейса.
    private void OnUiOpened(Entity<OrbitraBluespaceHarvesterComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSetEnabled(Entity<OrbitraBluespaceHarvesterComponent> ent, ref OrbitraBluespaceSetEnabledMessage args)
    {
        TrySetEnabled(ent, args.Enabled, args.Actor);
    }

    private void OnSetPowerMode(Entity<OrbitraBluespaceHarvesterComponent> ent, ref OrbitraBluespaceSetPowerModeMessage args)
    {
        TrySetPowerMode(ent, args.Mode, args.Actor);
    }

    private void OnSetLootCategory(Entity<OrbitraBluespaceHarvesterComponent> ent, ref OrbitraBluespaceSetLootCategoryMessage args)
    {
        TrySetLootCategory(ent, args.Category, args.Actor);
    }

    /// <summary>Переключает установку после проверки доступа и аварийной блокировки.</summary>
    public bool TrySetEnabled(Entity<OrbitraBluespaceHarvesterComponent> ent, bool enabled, EntityUid user)
    {
        if (!CanSetEnabled(ent, enabled, user))
            return false;

        ent.Comp.Enabled = enabled;
        UpdatePowerDraw(ent);
        UpdateAppearance(ent);
        UpdateUi(ent);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user)} set {ToPrettyString(ent)} enabled to {enabled}.");
        return true;
    }

    /// <summary>Проверяет доступ оператора и возможность запуска при аварийной остановке.</summary>
    public bool CanSetEnabled(Entity<OrbitraBluespaceHarvesterComponent> ent, bool enabled, EntityUid user)
    {
        return CanConfigure(ent, user) && (!enabled || !IsCritical(ent));
    }

    /// <summary>Выбирает один из двух режимов мощности без прямой записи в PowerState.</summary>
    public bool TrySetPowerMode(Entity<OrbitraBluespaceHarvesterComponent> ent, OrbitraBluespacePowerMode mode, EntityUid user)
    {
        if (!CanSetPowerMode(ent, mode, user))
            return false;

        ent.Comp.PowerMode = mode;
        UpdatePowerDraw(ent);
        UpdateUi(ent);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user)} set {ToPrettyString(ent)} power mode to {mode}.");
        return true;
    }

    /// <summary>Проверяет доступ оператора и допустимость значения режима мощности.</summary>
    public bool CanSetPowerMode(Entity<OrbitraBluespaceHarvesterComponent> ent, OrbitraBluespacePowerMode mode, EntityUid user)
    {
        return Enum.IsDefined(mode) && CanConfigure(ent, user);
    }

    /// <summary>Выбирает доступную категорию из настроенной таблицы наград.</summary>
    public bool TrySetLootCategory(Entity<OrbitraBluespaceHarvesterComponent> ent, OrbitraBluespaceLootCategory category, EntityUid user)
    {
        if (!CanSetLootCategory(ent, category, user))
            return false;

        ent.Comp.LootCategory = category;
        ent.Comp.OutputBlocked = false;
        UpdateUi(ent);
        return true;
    }

    /// <summary>Проверяет доступ, допустимость категории и наличие хотя бы одной награды в пределах лимита.</summary>
    public bool CanSetLootCategory(Entity<OrbitraBluespaceHarvesterComponent> ent, OrbitraBluespaceLootCategory category, EntityUid user)
    {
        return Enum.IsDefined(category) && CanConfigure(ent, user) && GetTotalLootWeight(category) > 0d;
    }

    /// <summary>Требует живого игрока вне паузы, способного взаимодействовать с открытым интерфейсом.</summary>
    public bool CanConfigure(Entity<OrbitraBluespaceHarvesterComponent> ent, EntityUid user)
    {
        return !TerminatingOrDeleted(ent) && !TerminatingOrDeleted(user) &&
               _metaQuery.TryComp(ent, out var machineMeta) && !machineMeta.EntityPaused &&
               _metaQuery.TryComp(user, out var userMeta) && !userMeta.EntityPaused &&
               _ui.IsUiOpen(ent.Owner, OrbitraBluespaceDeviceUiKey.Key, user) &&
               _actionBlocker.CanInteract(user, ent) && _actionBlocker.CanComplexInteract(user) &&
               _interaction.IsAccessible(user, ent.Owner) && _interaction.InRangeUnobstructed(user, ent.Owner);
    }

    private void UpdateUi(Entity<OrbitraBluespaceHarvesterComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, OrbitraBluespaceDeviceUiKey.Key))
            return;

        var received = 0f;
        var requested = 0f;
        if (_powerConsumerQuery.TryComp(ent, out var consumer))
        {
            received = float.IsFinite(consumer.ReceivedPower) ? MathF.Max(0f, consumer.ReceivedPower) : 0f;
            requested = float.IsFinite(consumer.DrawRate) ? MathF.Max(0f, consumer.DrawRate) : 0f;
        }

        var progress = double.IsFinite(ent.Comp.StoredEnergy) && float.IsFinite(ent.Comp.EnergyPerReward) &&
                       ent.Comp.EnergyPerReward > 0f
            ? (float) Math.Clamp(ent.Comp.StoredEnergy / ent.Comp.EnergyPerReward, 0d, 1d)
            : 0f;
        var anchored = _transformQuery.TryComp(ent, out var xform) && xform.Anchored;
        var state = new OrbitraBluespaceDeviceBoundUserInterfaceState(
            OrbitraBluespaceDeviceKind.Harvester,
            ent.Comp.Enabled,
            CanOperate(ent),
            anchored,
            _bluespaceBackground.TryGetScope(ent, out _),
            true,
            IsCritical(ent),
            _bluespaceBackground.GetLevel(ent),
            received,
            requested,
            progress,
            0f,
            ent.Comp.PowerMode,
            ent.Comp.LootCategory,
            ent.Comp.OutputBlocked,
            energyPerReward: ent.Comp.EnergyPerReward);
        _ui.SetUiState(ent.Owner, OrbitraBluespaceDeviceUiKey.Key, state);
    }
}
