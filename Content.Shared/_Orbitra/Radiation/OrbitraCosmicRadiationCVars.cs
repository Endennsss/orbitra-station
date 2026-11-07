// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.Configuration;

namespace Content.Shared._Orbitra.Radiation;

[CVarDefs]
public sealed class OrbitraCosmicRadiationCVars
{
    public static readonly CVarDef<float> Intensity = CVarDef.Create(
        "orbitra.cosmic_radiation_intensity", 0.3f, CVar.SERVERONLY | CVar.ARCHIVE);
}
