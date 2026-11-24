// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Shared._Orbitra.Radiation;

/// <summary>Проницаемость для космического излучения у надетого скафандра с герметичным шлемом.</summary>
[RegisterComponent]
public sealed partial class OrbitraCosmicRadiationProtectionComponent : Component
{
    /// <summary>Доля пропущенного излучения в диапазоне (0, 1], независимо от обычной радиационной брони.</summary>
    [DataField]
    public float Transmission = 0.2f;
}
