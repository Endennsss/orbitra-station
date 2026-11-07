// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Server._Orbitra.Radiation;

/// <summary>Preserves sub-hundredth doses between radiation updates.</summary>
[RegisterComponent]
public sealed partial class OrbitraCosmicRadiationDoseComponent : Component
{
    public double Remainder;
}
