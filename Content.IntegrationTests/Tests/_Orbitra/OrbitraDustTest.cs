using System.Numerics;
using System.IO;
using System.Linq;
using Content.Client._Orbitra.Particles;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Particles;
using Content.Shared._Orbitra.Particles;
using Content.Shared.Destructible;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.EntitySerialization.Systems;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraDustTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task ZoneLifecycleClampsMovesAndRemovesDerivedRegions()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        EntityUid marker = default;
        await server.WaitPost(() => marker = server.EntMan.SpawnEntity("OrbitraAmbientDustZone", map.GridCoords));
        await Pair.RunTicksSync(40);
        await server.WaitAssertion(() =>
        {
            var regions = server.EntMan.GetComponent<OrbitraAmbientDustGridComponent>(map.Grid).Regions;
            Assert.That(regions, Has.Count.EqualTo(1));
            Assert.That(regions[0].Size, Is.EqualTo(new Vector2(6)));
        });
        await server.WaitPost(() =>
        {
            var component = server.EntMan.GetComponent<OrbitraAmbientDustZoneComponent>(marker);
            component.Width = 99;
            component.Height = -3;
            server.System<SharedTransformSystem>().SetCoordinates(marker, new EntityCoordinates(map.Grid, 1, 0));
        });
        await Pair.RunTicksSync(40);
        await server.WaitAssertion(() =>
        {
            var zone = server.EntMan.GetComponent<OrbitraAmbientDustZoneComponent>(marker);
            Assert.That(zone.Width, Is.EqualTo(32));
            Assert.That(zone.Height, Is.EqualTo(1));
            Assert.That(server.EntMan.GetComponent<OrbitraAmbientDustGridComponent>(map.Grid).Regions[0].Center, Is.EqualTo(new Vector2(1, 0)));
        });
        await server.WaitPost(() => server.EntMan.DeleteEntity(marker));
        await Pair.RunTicksSync(40);
        await server.WaitAssertion(() => Assert.That(server.EntMan.GetComponent<OrbitraAmbientDustGridComponent>(map.Grid).Regions, Is.Empty));
    }

    [Test]
    public async Task ZoneMapRoundtripRebuildsRatherThanSerializingRegions()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        await server.WaitPost(() =>
        {
            var marker = server.EntMan.SpawnEntity("OrbitraAmbientDustZone", map.GridCoords);
            server.EntMan.GetComponent<OrbitraAmbientDustZoneComponent>(marker).Width = 13;
        });
        await Pair.RunTicksSync(40);
        EntityUid loadedGrid = default;
        await server.WaitAssertion(() =>
        {
            var loader = server.System<MapLoaderSystem>();
            using var writer = new StringWriter();
            Assert.That(loader.TrySaveMap(map.MapUid, writer), Is.True);
            var yaml = writer.ToString();
            Assert.That(yaml, Does.Not.Contain("regions:"));
            using var reader = new StringReader(yaml);
            Assert.That(loader.TryLoadMap(reader, "dust-roundtrip", out _, out var grids), Is.True);
            loadedGrid = grids!.Single().Owner;
        });
        await Pair.RunTicksSync(40);
        await server.WaitAssertion(() => Assert.That(server.EntMan.GetComponent<OrbitraAmbientDustGridComponent>(loadedGrid).Regions.Single().Width, Is.EqualTo(13)));
    }

    [Test]
    public async Task LampEligibilityUsesEnabledGlowAndGridIndex()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var client = Pair.Client;
        NetEntity lamp = default;
        await server.WaitPost(() =>
        {
            server.EntMan.SpawnEntity("OrbitraAmbientDustZone", map.GridCoords);
            lamp = server.EntMan.GetNetEntity(server.EntMan.SpawnEntity("AlwaysPoweredWallLight", map.GridCoords));
            var actor = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.GetSessionById(client.Session!.UserId), actor);
        });
        await Pair.RunTicksSync(40);
        await client.WaitAssertion(() =>
        {
            var uid = client.EntMan.GetEntity(lamp);
            var light = client.EntMan.GetComponent<Robust.Client.GameObjects.PointLightComponent>(uid);
            var particles = client.System<OrbitraParticleSystem>();
            Assert.That(particles.CanAmbient((uid, light), out var grid, out _), Is.True);
            var lights = client.System<Robust.Client.GameObjects.PointLightSystem>();
            lights.SetEnabled(uid, false, light);
            Assert.That(particles.CanAmbient((uid, light), out _, out _), Is.False);
            lights.SetEnabled(uid, true, light);
            client.EntMan.GetComponent<OrbitraAmbientDustGridComponent>(grid).Regions.Clear();
            Assert.That(particles.CanAmbient((uid, light), out _, out _), Is.False);
        });
    }

    [Test]
    public async Task ZoneEdgeIsAvailableWhenMarkerIsOutsidePvs()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var client = Pair.Client;
        NetEntity markerNet = default;
        NetEntity gridNet = default;
        await server.WaitPost(() =>
        {
            // Тестовый пул по умолчанию отключает пространственный PVS.
            server.ResolveDependency<Robust.Shared.Configuration.IConfigurationManager>().SetCVar(Robust.Shared.CVars.NetPVS, true);
            var maps = server.System<SharedMapSystem>();
            maps.SetTile(map.Grid, new Robust.Shared.Maths.Vector2i(32, 0), map.Tile.Tile);
            maps.SetTile(map.Grid, new Robust.Shared.Maths.Vector2i(48, 0), map.Tile.Tile);
            var marker = server.EntMan.SpawnEntity("OrbitraAmbientDustZone", new EntityCoordinates(map.Grid, 48.5f, 0.5f));
            server.EntMan.GetComponent<OrbitraAmbientDustZoneComponent>(marker).Width = 32;
            markerNet = server.EntMan.GetNetEntity(marker);
            gridNet = server.EntMan.GetNetEntity(map.Grid);
            var actor = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 32.5f, 0.5f));
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.GetSessionById(client.Session!.UserId), actor);
        });
        await Pair.RunTicksSync(50);
        await client.WaitAssertion(() =>
        {
            var grid = client.EntMan.GetEntity(gridNet);
            Assert.That(OrbitraDust.Contains(client.EntMan.GetComponent<OrbitraAmbientDustGridComponent>(grid).Regions, new Vector2(32.5f, 0.5f)), Is.True);
            Assert.That(!client.EntMan.TryGetEntity(markerNet, out var marker) || !client.EntMan.EntityExists(marker) ||
                (client.EntMan.GetComponent<MetaDataComponent>(marker!.Value).Flags & MetaDataFlags.Detached) != 0, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DestructionSurvivesMissingSourceButAdministrativeDeleteIsSilent(bool destroyed)
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var client = Pair.Client;
        EntityUid wall = default;
        await server.WaitPost(() =>
        {
            var gravity = server.EntMan.EnsureComponent<GravityComponent>(map.Grid);
            gravity.Enabled = gravity.Inherent = true;
            var actor = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            wall = server.EntMan.SpawnEntity("WallSolid", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.GetSessionById(client.Session!.UserId), actor);
        });
        await Pair.RunTicksSync(20);
        await client.WaitPost(() => { client.System<OrbitraParticleSystem>().PreviewQuality("Medium"); client.System<OrbitraParticleSystem>().Pool.Clear(); });
        await server.WaitPost(() =>
        {
            if (destroyed)
                server.System<DamageableSystem>().TryChangeDamage(wall,
                    new DamageSpecifier { DamageDict = { ["Blunt"] = 1000 } }, ignoreResistances: true);
            else
                server.EntMan.DeleteEntity(wall);
        });
        await Pair.RunTicksSync(4);
        await client.WaitAssertion(() => Assert.That(client.System<OrbitraParticleSystem>().Pool.Count, Is.EqualTo(destroyed ? 10 : 0)));
    }

    [TestCase("CrateGenericSteel", true, 0.3, 5)]
    [TestCase("CrateGenericSteel", true, 0.1, 0)]
    [TestCase("CrateGenericSteel", false, 0.3, 0)]
    [TestCase("Wrench", true, 0.3, 0)]
    [TestCase("MobHuman", true, 0.3, 0)]
    public async Task LandingHonorsSizeGravityDurationAndCooldown(string prototype, bool gravityEnabled, double duration, int count)
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var client = Pair.Client;
        EntityUid item = default;
        await server.WaitPost(() =>
        {
            var gravity = server.EntMan.EnsureComponent<GravityComponent>(map.Grid);
            gravity.Enabled = gravityEnabled;
            gravity.Inherent = true;
            var actor = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            item = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.GetSessionById(client.Session!.UserId), actor);
        });
        await Pair.RunTicksSync(20);
        await client.WaitPost(() => { client.System<OrbitraParticleSystem>().PreviewQuality("Medium"); client.System<OrbitraParticleSystem>().Pool.Clear(); });
        await server.WaitPost(() =>
        {
            // Проверяется контракт LandEvent; обычное выкладывание не имеет ThrownItem.
            var thrown = server.EntMan.EnsureComponent<ThrownItemComponent>(item);
            thrown.ThrownTime = server.ResolveDependency<IGameTiming>().CurTime - TimeSpan.FromSeconds(duration);
            thrown.Landed = true;
            var ev = new LandEvent(null, false);
            server.EntMan.EventBus.RaiseLocalEvent(item, ref ev);
            server.EntMan.EventBus.RaiseLocalEvent(item, ref ev);
            server.EntMan.RemoveComponent<ThrownItemComponent>(item);
        });
        await Pair.RunTicksSync(4);
        await client.WaitAssertion(() => Assert.That(client.System<OrbitraParticleSystem>().Pool.Count, Is.EqualTo(count)));
    }

    [Test]
    public async Task DedicatedSceneActuallyEmitsAmbientDust()
    {
        var map = await Pair.LoadTestMap(new Robust.Shared.Utility.ResPath("/Maps/_Orbitra/Test/dust.yml"));
        var client = Pair.Client;
        await Pair.Server.WaitPost(() =>
        {
            var actor = Pair.Server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 4.5f, 7.5f));
            Pair.Server.PlayerMan.SetAttachedEntity(Pair.Server.PlayerMan.GetSessionById(client.Session!.UserId), actor);
        });
        await Pair.RunTicksSync(40);
        await client.WaitAssertion(() =>
        {
            var system = client.System<OrbitraParticleSystem>();
            system.PreviewQuality("Medium");
            system.FrameUpdate(0f);
            system.Pool.Clear();
            using var viewport = client.ResolveDependency<Robust.Client.Graphics.IClyde>()
                .CreateViewport(new Robust.Shared.Maths.Vector2i(800, 600));
            var grid = client.EntMan.GetEntity(Pair.Server.EntMan.GetNetEntity(map.Grid));
            var xform = client.System<SharedTransformSystem>();
            var center = xform.ToMapCoordinates(new EntityCoordinates(grid, 8, 8));
            var bounds = Robust.Shared.Maths.Box2.CenteredAround(center.Position, new Vector2(14));
            var now = client.ResolveDependency<IGameTiming>().RealTime;
            for (var i = 0; i < 100; i++)
            {
                system.CollectAmbient(viewport, center.MapId, bounds, now + TimeSpan.FromSeconds(i * 0.1));
                system.Pool.Update(0.1f);
                // Два обновления без draw не должны терять таймер испускания.
                system.FrameUpdate(0f);
                system.FrameUpdate(0f);
            }
            Assert.That(system.Pool.AmbientCount(), Is.InRange(1, 32));
            system.PreviewQuality("Off");
            Assert.That(system.Pool.Count, Is.Zero);
        });
    }

    [Test]
    public async Task ZonePlacementPreviewAndFieldsVisibility()
    {
        var map = await Pair.CreateTestMap();
        var gridNet = Pair.Server.EntMan.GetNetEntity(map.Grid);
        await Pair.RunTicksSync(20);
        await Pair.Client.WaitAssertion(() =>
        {
            var client = Pair.Client;
            var system = client.System<Content.Client._Orbitra.Particles.OrbitraAmbientDustZoneSystem>();
            var markers = client.System<Content.Client.Markers.MarkerSystem>();
            markers.MarkersVisible = false;
            system.SetFieldsVisible(false);
            var preview = client.EntMan.SpawnEntity("OrbitraAmbientDustZone", MapCoordinates.Nullspace);
            Assert.That(client.EntMan.GetComponent<Robust.Client.GameObjects.SpriteComponent>(preview).Visible, Is.True);
            var coords = new EntityCoordinates(client.EntMan.GetEntity(gridNet), Vector2.Zero);
            var zone = client.EntMan.SpawnEntity("OrbitraAmbientDustZone", coords);
            var other = client.EntMan.SpawnEntity("SpawnPointLatejoin", coords);
            Assert.That(client.EntMan.GetComponent<Robust.Client.GameObjects.SpriteComponent>(zone).Visible, Is.False);
            system.SetFieldsVisible(true);
            Assert.That(client.EntMan.GetComponent<Robust.Client.GameObjects.SpriteComponent>(zone).Visible, Is.True);
            Assert.That(client.EntMan.GetComponent<Robust.Client.GameObjects.SpriteComponent>(other).Visible, Is.False);
            system.SetFieldsVisible(false);
            Assert.That(client.EntMan.GetComponent<Robust.Client.GameObjects.SpriteComponent>(zone).Visible, Is.False);
            Assert.That(client.EntMan.GetComponent<Robust.Client.GameObjects.SpriteComponent>(preview).Visible, Is.True);
        });
    }

    [Test]
    public async Task DedicatedSceneLoadsFiftyLampsAndDerivedZones()
    {
        var map = await Pair.LoadTestMap(new Robust.Shared.Utility.ResPath("/Maps/_Orbitra/Test/dust.yml"));
        await Pair.RunTicksSync(40);
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Pair.Server.EntMan.GetComponent<OrbitraAmbientDustGridComponent>(map.Grid).Regions, Has.Count.EqualTo(2));
            var query = Pair.Server.EntMan.EntityQueryEnumerator<Robust.Server.GameObjects.PointLightComponent, TransformComponent>();
            var count = 0;
            while (query.MoveNext(out _, out var xform))
                if (xform.GridUid == map.Grid.Owner)
                    count++;
            Assert.That(count, Is.EqualTo(50));
        });
    }

    [TestCase(4f, -4f, 5)]
    [TestCase(4f, 4f, 0)]
    [TestCase(0.5f, -0.5f, 0)]
    public async Task PhysicalContactUsesRelativeSpeedAndEmitsOnce(float firstSpeed, float secondSpeed, int expected)
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var client = Pair.Client;
        EntityUid first = default;
        EntityUid second = default;
        await server.WaitPost(() =>
        {
            var gravity = server.EntMan.EnsureComponent<GravityComponent>(map.Grid);
            gravity.Enabled = gravity.Inherent = true;
            var actor = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 0, 1.4f));
            first = server.EntMan.SpawnEntity("CrateGenericSteel", new EntityCoordinates(map.Grid, -0.8f, 0));
            second = server.EntMan.SpawnEntity("CrateGenericSteel", new EntityCoordinates(map.Grid, 0.8f, 0));
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.GetSessionById(client.Session!.UserId), actor);
        });
        await Pair.RunTicksSync(20);
        await client.WaitPost(() => { client.System<OrbitraParticleSystem>().PreviewQuality("Medium"); client.System<OrbitraParticleSystem>().Pool.Clear(); });
        await server.WaitPost(() =>
        {
            var physics = server.System<SharedPhysicsSystem>();
            physics.SetLinearVelocity(first, new Vector2(firstSpeed, 0));
            physics.SetLinearVelocity(second, new Vector2(secondSpeed, 0));
        });
        await Pair.RunTicksSync(8);
        await client.WaitAssertion(() => Assert.That(client.System<OrbitraParticleSystem>().Pool.Count, Is.EqualTo(expected)));
        if (expected > 0)
        {
            await server.WaitAssertion(() =>
            {
                var dust = server.System<OrbitraParticleBurstSystem>();
                Assert.That(dust.TryImpactDust(first), Is.False, "Contact and landing must share the cooldown.");
                Assert.That(dust.TryImpactDust(second), Is.False, "Both sides of the contact are deduplicated.");
            });
        }
    }
}
