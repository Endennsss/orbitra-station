// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Server._Orbitra.Radiation;

/// <summary>Накапливает дозы меньше сотой между обновлениями радиации.</summary>
[RegisterComponent]
public sealed partial class OrbitraCosmicRadiationDoseComponent : Component
{
    public double Remainder;
}
