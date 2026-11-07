// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Server._Orbitra.Xenobiology;

/// <summary>Safely contains laboratory slimes without making living mobs into items.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologyCarrierComponent : Component
{
    /// <summary>Container used for specimens.</summary>
    [DataField]
    public string Container = "xeno_carrier";

    /// <summary>Maximum carried specimens.</summary>
    [DataField]
    public int Capacity = 1;

    /// <summary>Time needed to capture one reachable specimen.</summary>
    [DataField]
    public TimeSpan CaptureDelay = TimeSpan.FromSeconds(2);
}
