// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Server._Orbitra.Ghost.Skins;
using Content.Server.Mind;
using Content.Shared._Orbitra.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Orbitra.Ghost;

[TestFixture, NonParallelizable]
public sealed class OrbitraGhostSkinSecurityTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task NonGhostOrDetachedSenderCannotChangeAnyObserver(bool detached)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var previous = player.AttachedEntity;
        EntityUid human = default, otherGhost = default;
        try
        {
            await server.WaitPost(() =>
            {
                human = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
                otherGhost = server.EntMan.SpawnEntity("MobObserver", map.GridCoords);
                server.PlayerMan.SetAttachedEntity(player, detached ? null : human);
            });
            await pair.RunTicksSync(15);
            await pair.Client.WaitPost(() => pair.Client.ResolveDependency<IEntityNetworkManager>()
                .SendSystemNetworkMessage(new OrbitraSelectGhostSkinEvent("OrbitraGhostSkinMauler")));
            await pair.RunTicksSync(10);
            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.HasComponent<OrbitraGhostSkinComponent>(human), Is.False);
                Assert.That(server.EntMan.GetComponent<OrbitraGhostSkinComponent>(otherGhost).Skin.Id,
                    Is.EqualTo(OrbitraGhostSkinComponent.DefaultSkin));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.PlayerMan.SetAttachedEntity(player, previous);
                server.EntMan.DeleteEntity(map.MapUid);
            });
            await pair.RunTicksSync(2);
        }
        await pair.CleanReturnAsync();
    }

    [TestCase("OrbitraGhostSkinDoesNotExist")]
    [TestCase("")]
    [TestCase("   ")]
    public async Task InvalidNetworkSkinDoesNotAddAComponent(string skin)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var previous = player.AttachedEntity;
        EntityUid ghost = default;
        try
        {
            await server.WaitPost(() =>
            {
                ghost = server.EntMan.SpawnEntity("MobObserver", map.GridCoords);
                server.PlayerMan.SetAttachedEntity(player, ghost);
                server.EntMan.RemoveComponent<OrbitraGhostSkinComponent>(ghost);
            });
            await pair.RunTicksSync(15);
            await pair.Client.WaitPost(() => pair.Client.ResolveDependency<IEntityNetworkManager>()
                .SendSystemNetworkMessage(new OrbitraSelectGhostSkinEvent(skin)));
            await pair.RunTicksSync(10);
            await server.WaitAssertion(() =>
            {
                Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
                Assert.That(server.EntMan.HasComponent<GhostComponent>(ghost), Is.True);
                Assert.That(server.EntMan.HasComponent<OrbitraGhostSkinComponent>(ghost), Is.False);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.PlayerMan.SetAttachedEntity(player, previous);
                server.EntMan.DeleteEntity(map.MapUid);
            });
            await pair.RunTicksSync(2);
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ValidationIsPureAndRepeatedSelectionDoesNotDirtyTheComponent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid ghost = default;
        bool canSelect = false, acceptedNull = true, accepted = false, repeated = false;
        GameTick changedTick = default;
        try
        {
            await server.WaitPost(() =>
            {
                ghost = server.EntMan.SpawnEntity("MobObserver", map.GridCoords);
                server.EntMan.RemoveComponent<OrbitraGhostSkinComponent>(ghost);
                var skins = server.System<OrbitraGhostSkinSystem>();
                canSelect = skins.CanSetSkin(ghost, "OrbitraGhostSkinMauler");
                acceptedNull = skins.TrySetSkin(ghost, default(ProtoId<OrbitraGhostSkinPrototype>));
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(canSelect, Is.True);
                Assert.That(acceptedNull, Is.False);
                Assert.That(server.EntMan.HasComponent<OrbitraGhostSkinComponent>(ghost), Is.False);
            });
            await server.WaitPost(() =>
            {
                accepted = server.System<OrbitraGhostSkinSystem>().TrySetSkin(ghost, "OrbitraGhostSkinMauler");
                changedTick = server.EntMan.GetComponent<OrbitraGhostSkinComponent>(ghost).LastModifiedTick;
            });
            await pair.RunTicksSync(2);
            await server.WaitPost(() => repeated = server.System<OrbitraGhostSkinSystem>()
                .TrySetSkin(ghost, "OrbitraGhostSkinMauler"));
            await server.WaitAssertion(() =>
            {
                Assert.That(accepted && repeated, Is.True);
                Assert.That(server.EntMan.GetComponent<OrbitraGhostSkinComponent>(ghost).LastModifiedTick,
                    Is.EqualTo(changedTick));
            });
        }
        finally
        {
            await server.WaitPost(() => server.EntMan.DeleteEntity(map.MapUid));
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NetworkSelectionAndResetPreserveGhostAndRealMechCapabilities()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var previous = player.AttachedEntity;
        EntityUid ghost = default, mech = default, mind = default;
        object ghostState = null!, mechState = null!;
        try
        {
            await server.WaitPost(() =>
            {
                ghost = server.EntMan.SpawnEntity("MobObserver", map.GridCoords);
                mech = server.EntMan.SpawnEntity("MechRipley", map.GridCoords);
                var minds = server.System<MindSystem>();
                mind = minds.CreateMind(null);
                minds.TransferTo(mind, ghost);
                server.PlayerMan.SetAttachedEntity(player, ghost);
            });
            await pair.RunTicksSync(15);
            await server.WaitPost(() =>
            {
                ghostState = CaptureGhost(server.EntMan, ghost);
                mechState = CaptureMech(server.EntMan, mech);
            });
            foreach (var skin in new[] { "OrbitraGhostSkinMauler", OrbitraGhostSkinComponent.DefaultSkin })
            {
                await pair.Client.WaitPost(() => pair.Client.ResolveDependency<IEntityNetworkManager>()
                    .SendSystemNetworkMessage(new OrbitraSelectGhostSkinEvent(skin)));
                await pair.RunTicksSync(10);
                await server.WaitAssertion(() =>
                {
                    Assert.That(server.EntMan.GetComponent<OrbitraGhostSkinComponent>(ghost).Skin.Id, Is.EqualTo(skin));
                    Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
                    Assert.That(CaptureGhost(server.EntMan, ghost), Is.EqualTo(ghostState));
                    Assert.That(server.EntMan.GetComponent<MindComponent>(mind).OwnedEntity, Is.EqualTo(ghost));
                    Assert.That(server.EntMan.GetComponent<MindComponent>(mind).CurrentEntity, Is.EqualTo(ghost));
                    Assert.That(CaptureMech(server.EntMan, mech), Is.EqualTo(mechState));
                    Assert.That(server.EntMan.HasComponent<MechComponent>(ghost), Is.False);
                    Assert.That(server.EntMan.HasComponent<OrbitraGhostSkinComponent>(mech), Is.False);
                });
            }
            await server.WaitPost(() => server.PlayerMan.SetAttachedEntity(player, previous));
            await pair.RunTicksSync(2);
            await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(ghost), Is.False));
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.PlayerMan.SetAttachedEntity(player, previous);
                server.EntMan.DeleteEntity(map.MapUid);
                if (server.EntMan.EntityExists(mind))
                    server.EntMan.DeleteEntity(mind);
            });
            await pair.RunTicksSync(2);
        }
        await pair.CleanReturnAsync();
    }

    private static object CaptureGhost(IEntityManager entities, EntityUid ghost)
    {
        var physics = entities.GetComponent<PhysicsComponent>(ghost);
        var observer = entities.GetComponent<GhostComponent>(ghost);
        var mind = entities.GetComponent<MindContainerComponent>(ghost);
        return (entities.GetComponent<MetaDataComponent>(ghost).EntityPrototype?.ID,
            physics.CanCollide, physics.BodyType, physics.CollisionLayer, physics.CollisionMask,
            entities.GetComponent<VisibilityComponent>(ghost).Layer,
            entities.GetComponent<EyeComponent>(ghost).VisibilityMask,
            observer.CanGhostInteract, observer.CanReturnToBody, observer.TimeOfDeath,
            mind.Mind, mind.HasMind, mind.LastMind);
    }

    private static object CaptureMech(IEntityManager entities, EntityUid mech)
    {
        var physics = entities.GetComponent<PhysicsComponent>(mech);
        var vehicle = entities.GetComponent<MechComponent>(mech);
        return (entities.GetComponent<MetaDataComponent>(mech).EntityPrototype?.ID,
            physics.CanCollide, physics.BodyType, physics.CollisionLayer, physics.CollisionMask,
            vehicle.Integrity, vehicle.MaxIntegrity, vehicle.Broken,
            vehicle.MaxEquipmentAmount, vehicle.CurrentSelectedEquipment);
    }
}
