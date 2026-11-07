// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Server._Orbitra.Research.Bluespace;

/// <summary>Configures an APC-powered suppressor that remains usable during a station scram.</summary>
[RegisterComponent, Access(typeof(OrbitraBluespaceResearchSystem))]
public sealed partial class OrbitraBluespaceSuppressorComponent : Component
{
    /// <summary>Requested operating state; power, anchoring and station membership remain required.</summary>
    [DataField]
    public bool Enabled;

    /// <summary>Station background removed per active second.</summary>
    [DataField]
    public float SuppressionPerSecond = 0.12f;

    internal float UiElapsed = 1f;
}
