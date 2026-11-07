// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.Configuration;

namespace Content.Shared._Orbitra.Ghost;

[CVarDefs]
public sealed class OrbitraGhostSkinCVars
{
    public static readonly CVarDef<string> SelectedSkin = CVarDef.Create(
        "orbitra.ghost_skin", OrbitraGhostSkinComponent.DefaultSkin, CVar.CLIENTONLY | CVar.ARCHIVE);
}
