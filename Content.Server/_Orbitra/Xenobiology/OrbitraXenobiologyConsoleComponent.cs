// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Server._Orbitra.Xenobiology;

/// <summary>Displays the local colony's measurements and the breed reference catalog.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologyConsoleComponent : Component
{
    /// <summary>Maximum measurement distance in world metres.</summary>
    [DataField]
    public float Range = 20f;
}
