// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Shared._Orbitra.Radiation;

/// <summary>Transmission of cosmic radiation through an equipped suit with a sealed helmet.</summary>
[RegisterComponent]
public sealed partial class OrbitraCosmicRadiationProtectionComponent : Component
{
    /// <summary>Fraction transmitted, independent of conventional radiation armor. Must be in (0, 1].</summary>
    [DataField]
    public float Transmission = 0.2f;
}
