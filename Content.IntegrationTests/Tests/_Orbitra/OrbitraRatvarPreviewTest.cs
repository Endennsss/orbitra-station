using System;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.Administration.Managers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Admin manifestation requires explicit authorization and never creates a winning rule.</summary>
[TestFixture]
public sealed class OrbitraRatvarPreviewTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [Test]
    public async Task PreviewMovesOnlyWhileAuthorizedAndKeepsCultIndependent()
    {
        var map = await Pair.CreateTestMap();
        Entity<OrbitraRatvarManifestationComponent> god = default;
        var started = false;
        EntityCoordinates before = default;
        var system = Server.System<OrbitraRatvarManifestationSystem>();
        await Server.WaitPost(() =>
        {
            var mapping = Server.System<SharedMapSystem>();
            for (var x = -2; x <= 2; x++)
            for (var y = -2; y <= 2; y++)
                mapping.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            var uid = SEntMan.SpawnEntity("OrbitraRatvarPreview", map.GridCoords);
            god = (uid, SEntMan.GetComponent<OrbitraRatvarManifestationComponent>(uid));
            before = SEntMan.GetComponent<TransformComponent>(uid).Coordinates;
            Server.Resolve<IAdminManager>().PromoteHost(ServerSession!);
            Server.Resolve<IAdminManager>().DeAdmin(ServerSession!);
            started = system.TryTogglePreview(god, ServerSession!);
        });
        await Server.WaitAssertion(() => Assert.That(started, Is.False));
        await Server.WaitPost(() =>
        {
            Server.Resolve<IAdminManager>().ReAdmin(ServerSession!);
            started = system.TryTogglePreview(god, ServerSession!);
            system.Update(0);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(god).Coordinates, Is.Not.EqualTo(before));
            Assert.That(god.Comp.Rule, Is.Null);
            Assert.That(SEntMan.Count<OrbitraRatvarRuleComponent>(), Is.Zero);
        });
        await Server.WaitPost(() => system.TryTogglePreview(god, ServerSession!));
        await Server.WaitAssertion(() => Assert.That(system.CanAct(god, out _), Is.False));
        await Server.WaitPost(() =>
        {
            system.TryTogglePreview(god, ServerSession!);
            god.Comp.PreviewUntil = Server.Resolve<IGameTiming>().CurTime - TimeSpan.FromSeconds(1);
        });
        await Server.WaitAssertion(() => Assert.That(system.CanAct(god, out _), Is.False));
    }
}
