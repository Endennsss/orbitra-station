// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.Inventory;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ghost;

public sealed partial class OrbitraAdminGhostInventorySystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private static readonly ProtoId<InventoryTemplatePrototype> BaseTemplate = "aghost";
    private static readonly ProtoId<InventoryTemplatePrototype> ExtendedTemplate = "OrbitraAdminGhost";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        _admin.OnPermsChanged += OnAdminPermsChanged;
    }

    public override void Shutdown()
    {
        _admin.OnPermsChanged -= OnAdminPermsChanged;
        base.Shutdown();
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        RefreshSlots(args.Entity, args.Player);
    }

    private void OnAdminPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player.AttachedEntity is { } uid)
            RefreshSlots(uid, args.Player);
    }

    private void RefreshSlots(EntityUid uid, ICommonSession player)
    {
        if (!TryComp(uid, out MetaDataComponent? metadata) ||
            metadata.EntityPrototype?.ID != GameTicker.AdminObserverPrototypeName.Id ||
            !TryComp(uid, out InventoryComponent? inventory))
        {
            return;
        }

        var template = _admin.HasAdminFlag(player, AdminFlags.Admin) ? ExtendedTemplate : BaseTemplate;
        _inventory.SetTemplateId((uid, inventory), template);
    }
}
