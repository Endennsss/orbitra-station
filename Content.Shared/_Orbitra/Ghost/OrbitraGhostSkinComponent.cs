// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Ghost;

/// <summary>The server-selected appearance replicated to observers; it grants no gameplay capabilities.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true, fieldDeltas: true)]
public sealed partial class OrbitraGhostSkinComponent : Component
{
    public const string DefaultSkin = "OrbitraGhostSkinDefault";
    [DataField, AutoNetworkedField]
    public ProtoId<OrbitraGhostSkinPrototype> Skin = DefaultSkin;
}
