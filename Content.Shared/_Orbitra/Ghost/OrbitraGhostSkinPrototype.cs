// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Orbitra.Ghost;

/// <summary>Косметический облик наблюдателя. Если спрайт отсутствует, возвращается штатный вид.</summary>
[Prototype]
public sealed partial class OrbitraGhostSkinPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public LocId Name;
    [DataField(required: true)] public LocId Description;
    [DataField] public SpriteSpecifier? Sprite;
    [DataField(required: true)] public SpriteSpecifier Icon = default!;
    /// <summary>Непрозрачность заменяющего спрайта, независимая от прозрачности исходного призрака.</summary>
    [DataField] public float Opacity = 0.5f;
    [DataField] public int Order;
}
