// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared._Orbitra.Ghost;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Ghost.Skins;

/// <summary>Client-only restoration data for a cosmetic ghost sprite replacement.</summary>
[RegisterComponent]
public sealed partial class OrbitraGhostSkinVisualComponent : Component
{
    /// <summary>The native sprite tint captured before replacement.</summary>
    public Color OriginalColor;

    /// <summary>The native ghost layer visibility captured before replacement.</summary>
    public bool OriginalVisible;

    /// <summary>Whether a replacement currently owns the native layer visibility and global tint.</summary>
    public bool Replacing;

    /// <summary>The replacement whose layer data was last applied.</summary>
    public ProtoId<OrbitraGhostSkinPrototype>? AppliedSkin;
}
