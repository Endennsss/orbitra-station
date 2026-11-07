// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;

namespace Content.Shared.Research.Systems;

public abstract partial class SharedResearchSystem
{
    /// <summary>
    /// Resolves the configured ceiling for disciplines governed only by explicit technology prerequisites.
    /// </summary>
    private static bool TryGetOrbitraUnrestrictedTier(TechDisciplinePrototype discipline, out int tier)
    {
        tier = 0;
        if (discipline.LockoutTier != int.MaxValue || discipline.TierPrerequisites.Count == 0)
            return false;

        foreach (var (configuredTier, threshold) in discipline.TierPrerequisites)
        {
            if (threshold != 0f)
                return false;

            tier = Math.Max(tier, configuredTier);
        }

        return true;
    }

    private void UpdateOrbitraTechnologyChoices(EntityUid uid, TechnologyDatabaseComponent database)
    {
        var available = GetAvailableTechnologies(uid, database);
        available.Sort(static (left, right) => string.Compare(left.ID, right.ID, StringComparison.Ordinal));
        database.CurrentTechnologyCards.Clear();
        foreach (var technology in available)
            database.CurrentTechnologyCards.Add(technology.ID);
        Dirty(uid, database);
    }
}
