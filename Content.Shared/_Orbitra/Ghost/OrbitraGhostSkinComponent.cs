// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Ghost;

/// <summary>Выбранный сервером косметический облик призрака, передаваемый наблюдателям.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true, fieldDeltas: true)]
public sealed partial class OrbitraGhostSkinComponent : Component
{
    public const string DefaultSkin = "OrbitraGhostSkinDefault";
    [DataField, AutoNetworkedField]
    public ProtoId<OrbitraGhostSkinPrototype> Skin = DefaultSkin;
}
