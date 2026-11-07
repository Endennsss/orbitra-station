// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Orbitra.Ghost;

/// <summary>A cosmetic observer appearance. An absent sprite restores the native ghost appearance.</summary>
[Prototype("orbitraGhostSkin")]
public sealed partial class OrbitraGhostSkinPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public LocId Name;
    [DataField(required: true)] public LocId Description;
    [DataField] public SpriteSpecifier? Sprite;
    [DataField(required: true)] public SpriteSpecifier Icon = default!;
    /// <summary>Opacity of the replacement sprite, independent of the native ghost's alpha.</summary>
    [DataField] public float Opacity = 0.5f;
    [DataField] public int Order;
}
