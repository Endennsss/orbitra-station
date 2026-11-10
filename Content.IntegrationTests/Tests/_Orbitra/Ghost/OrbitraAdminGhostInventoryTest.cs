// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.IntegrationTests.Fixtures;
using Content.Server.Administration.Managers;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Orbitra.Ghost;

public sealed class OrbitraAdminGhostInventoryTest : GameTest
{
    [Test]
    public async Task ExtraEquipmentSlotsFollowAdminPermissions()
    {
        var map = await Pair.CreateTestMap();
        var player = ServerSession!;
        var admin = Server.ResolveDependency<IAdminManager>();
        var inventory = Server.System<InventorySystem>();
        var containers = Server.System<SharedContainerSystem>();
        var equipment = new (string Slot, string Prototype)[]
        {
            ("pocket1", "Screwdriver"),
            ("pocket2", "Crowbar"),
            ("belt", "ClothingBeltUtility"),
        };
        EntityUid observer = default;
        EntityUid adminGhost = default;
        EntityUid? originalBackpack = null;
        var items = new EntityUid[equipment.Length];

        await Server.WaitAssertion(() => Assert.That(admin.HasAdminFlag(player, AdminFlags.Admin), Is.True));
        await Server.WaitPost(() =>
        {
            observer = SEntMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(player, observer);
        });
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<InventoryComponent>(observer), Is.False));

        await Server.WaitPost(() =>
        {
            adminGhost = SEntMan.SpawnEntity(GameTicker.AdminObserverPrototypeName, map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(player, adminGhost);
            inventory.TryGetSlotEntity(adminGhost, "back", out originalBackpack);
            for (var i = 0; i < equipment.Length; i++)
            {
                var (slot, prototype) = equipment[i];
                items[i] = SEntMan.SpawnEntity(prototype, map.GridCoords);
                inventory.TryEquip(adminGhost, items[i], slot, silent: true);
            }
        });
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(originalBackpack, Is.Not.Null);
                for (var i = 0; i < equipment.Length; i++)
                {
                    var slot = equipment[i].Slot;
                    Assert.That(inventory.TryGetSlotEntity(adminGhost, slot, out var item), Is.True, slot);
                    Assert.That(item, Is.EqualTo(items[i]), slot);
                }
            });
        });

        try
        {
            await Server.WaitPost(() => admin.DeAdmin(player));
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(player.AttachedEntity, Is.EqualTo(adminGhost));
                    Assert.That(inventory.TryGetSlotEntity(adminGhost, "back", out var backpack), Is.True);
                    Assert.That(backpack, Is.EqualTo(originalBackpack));
                    for (var i = 0; i < equipment.Length; i++)
                    {
                        var slot = equipment[i].Slot;
                        Assert.That(inventory.HasSlot(adminGhost, slot), Is.False, slot);
                        Assert.That(containers.TryGetContainer(adminGhost, slot, out _), Is.False, slot);
                        Assert.That(SEntMan.EntityExists(items[i]), Is.True, slot);
                        Assert.That(containers.IsEntityInContainer(items[i]), Is.False, slot);
                    }
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => admin.ReAdmin(player));
        }

        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var (slot, _) in equipment)
                {
                    Assert.That(inventory.HasSlot(adminGhost, slot), Is.True, slot);
                    Assert.That(inventory.TryGetSlotEntity(adminGhost, slot, out _), Is.False, slot);
                }
            });
        });
    }
}
