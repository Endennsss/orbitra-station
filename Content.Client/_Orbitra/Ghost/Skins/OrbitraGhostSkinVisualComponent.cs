// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared._Orbitra.Ghost;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Ghost.Skins;

/// <summary>Клиентские данные для восстановления спрайта после смены облика призрака.</summary>
[RegisterComponent]
public sealed partial class OrbitraGhostSkinVisualComponent : Component
{
    /// <summary>Исходный оттенок спрайта до замены.</summary>
    public Color OriginalColor;

    /// <summary>Исходная видимость слоя призрака до замены.</summary>
    public bool OriginalVisible;

    /// <summary>Управляет ли заменяющий облик видимостью слоя и общим оттенком.</summary>
    public bool Replacing;

    /// <summary>Облик, параметры которого последними применены к слою.</summary>
    public ProtoId<OrbitraGhostSkinPrototype>? AppliedSkin;
}
