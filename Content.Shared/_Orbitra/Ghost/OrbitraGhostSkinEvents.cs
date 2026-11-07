// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ghost;

/// <summary>Requests a predefined skin for the sender's currently attached ghost.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraSelectGhostSkinEvent(ProtoId<OrbitraGhostSkinPrototype> skin) : EntityEventArgs
{
    public ProtoId<OrbitraGhostSkinPrototype> Skin = skin;
}
