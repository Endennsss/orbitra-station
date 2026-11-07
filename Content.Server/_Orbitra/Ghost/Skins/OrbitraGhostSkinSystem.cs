// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared._Orbitra.Ghost;
using Content.Shared.Ghost.Components;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ghost.Skins;

/// <summary>Validates cosmetic skin requests without changing observer capabilities.</summary>
public sealed partial class OrbitraGhostSkinSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<OrbitraSelectGhostSkinEvent>(OnSelectGhostSkin);
    }

    private void OnSelectGhostSkin(OrbitraSelectGhostSkinEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!session.Channel.IsConnected || session.Status != SessionStatus.InGame)
            return;

        if (session.AttachedEntity is not { } ghost)
            return;

        TrySetSkin(ghost, msg.Skin);
    }

    /// <summary>Checks the target and predefined appearance without adding or changing components.</summary>
    public bool CanSetSkin(EntityUid ghost, ProtoId<OrbitraGhostSkinPrototype> skin)
    {
        if (Deleted(ghost) || Terminating(ghost) || !HasComp<GhostComponent>(ghost))
            return false;

        if (string.IsNullOrWhiteSpace(skin.Id) || !_prototype.TryIndex(skin, out var prototype))
            return false;

        return prototype.Sprite == null ||
               float.IsFinite(prototype.Opacity) && prototype.Opacity > 0f && prototype.Opacity <= 1f;
    }

    /// <summary>Selects a validated skin for an observer.</summary>
    public bool TrySetSkin(EntityUid ghost, ProtoId<OrbitraGhostSkinPrototype> skin)
    {
        if (!CanSetSkin(ghost, skin))
            return false;

        SetSkin(ghost, skin);
        return true;
    }

    private void SetSkin(EntityUid ghost, ProtoId<OrbitraGhostSkinPrototype> skin)
    {
        var component = EnsureComp<OrbitraGhostSkinComponent>(ghost);
        if (component.Skin == skin)
            return;

        component.Skin = skin;
        DirtyField(ghost, component, nameof(OrbitraGhostSkinComponent.Skin));
    }
}
