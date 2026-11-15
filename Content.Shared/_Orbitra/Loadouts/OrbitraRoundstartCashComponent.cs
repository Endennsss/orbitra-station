// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

namespace Content.Shared._Orbitra.Loadouts;

/// <summary>
/// Rolls a random amount of credits when the stack is spawned, for the roundstart cash loadout.
/// </summary>
[RegisterComponent]
public sealed partial class OrbitraRoundstartCashComponent : Component
{
    [DataField]
    public int Min = 100;

    [DataField]
    public int Max = 4000;
}
