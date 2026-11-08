// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared._Orbitra.Ghost;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Orbitra.Ghost;

[TestFixture, NonParallelizable]
public sealed class OrbitraGhostSkinTest
{
    /// <summary>The owning observer can request a predefined skin through the actual network boundary.</summary>
    [TestCase("OrbitraGhostSkinMauler")]
    [TestCase("OrbitraGhostSkinHoloCarp")]
    [TestCase("OrbitraGhostSkinDragon")]
    public async Task AttachedGhostCanSelectPredefinedSkinThroughNetwork(string skin)
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
            });
            await pair.RunTicksSync(15);
            await pair.Client.WaitPost(() => pair.Client.ResolveDependency<IEntityNetworkManager>()
                .SendSystemNetworkMessage(new OrbitraSelectGhostSkinEvent(skin)));
            await pair.RunTicksSync(10);
            await server.WaitAssertion(() => Assert.That(
                server.EntMan.GetComponent<OrbitraGhostSkinComponent>(ghost).Skin.Id,
                Is.EqualTo(skin)));
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
}
