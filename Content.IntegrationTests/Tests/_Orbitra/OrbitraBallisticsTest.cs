using System.Numerics;
using Content.Client._Orbitra.Particles;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Particles;
using Content.Shared.Damage;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraBallisticsTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task ConfirmedBulletBurstSurvivesTargetDeletion(bool deleteTarget)
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var map = await Pair.CreateTestMap();
        EntityUid wall = default;
        EntityUid actor = default;
        await server.WaitPost(() =>
        {
            actor = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            wall = server.EntMan.SpawnEntity("WallSolid", new EntityCoordinates(map.GridCoords.EntityId,
                map.GridCoords.Position + Vector2.UnitX));
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.GetSessionById(client.Session!.UserId), actor);
        });
        await Pair.RunTicksSync(20);
        await client.WaitPost(() =>
        {
            client.System<OrbitraParticleSystem>().PreviewQuality("Medium");
            client.System<OrbitraParticleSystem>().Pool.Clear();
        });
        var emitted = true;
        await server.WaitPost(() =>
        {
            var system = server.System<OrbitraParticleBurstSystem>();
            var impact = system.CaptureBulletImpact(wall, actor, true);
            emitted = system.TryBulletImpact(impact, new DamageSpecifier());
        });
        Assert.That(emitted, Is.False, "Zero damage must not emit particles.");
        await Pair.RunTicksSync(4);
        await client.WaitAssertion(() => Assert.That(client.System<OrbitraParticleSystem>().Pool.Count, Is.Zero));
        await server.WaitPost(() =>
        {
            var system = server.System<OrbitraParticleBurstSystem>();
            var impact = system.CaptureBulletImpact(wall, actor, true);
            if (deleteTarget)
                server.EntMan.DeleteEntity(wall);
            emitted = system.TryBulletImpact(impact, new DamageSpecifier { DamageDict = { ["Piercing"] = 10 } });
        });
        Assert.That(emitted, Is.True);
        await Pair.RunTicksSync(4);
        await client.WaitAssertion(() =>
        {
            var pool = client.System<OrbitraParticleSystem>().Pool;
            var chips = 0;
            for (var i = 0; i < pool.Count; i++)
                if (pool.Particles[i].Effect.ID == "OrbitraParticleBulletMetal")
                    chips++;
            Assert.That(chips, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task MuzzleDoesNotModifyPermanentLightAndReusesOwnedLight()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netActor = default;
        await Pair.Server.WaitPost(() =>
        {
            var actor = Pair.Server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            netActor = Pair.Server.EntMan.GetNetEntity(actor);
            Pair.Server.PlayerMan.SetAttachedEntity(Pair.Server.PlayerMan.GetSessionById(Pair.Client.Session!.UserId), actor);
        });
        await Pair.RunTicksSync(20);
        var preserved = false;
        var reused = false;
        await Pair.Client.WaitPost(() =>
        {
            var entities = Pair.Client.EntMan;
            var actor = entities.GetEntity(netActor);
            var coordinates = entities.GetComponent<TransformComponent>(actor).Coordinates;
            var permanent = entities.SpawnEntity(null, coordinates);
            var light = entities.AddComponent<PointLightComponent>(permanent);
            var lights = Pair.Client.System<PointLightSystem>();
            lights.SetColor(permanent, Color.Red, light);
            lights.SetRadius(permanent, 7, light);
            lights.SetEnergy(permanent, 2, light);
            lights.SetEnabled(permanent, true, light);
            var system = Pair.Client.System<OrbitraGunEffectsSystem>();
            system.ObserveShot(permanent, actor, Angle.Zero, "MuzzleFlashEffect");
            preserved = light.Color == Color.Red && light.Radius == 7 && light.Energy == 2 && light.Enabled;

            var transient = entities.SpawnEntity(null, coordinates);
            system.ObserveShot(transient, actor, Angle.Zero, "MuzzleFlashEffect");
            var first = entities.GetComponent<PointLightComponent>(transient);
            for (var i = 0; i < 10; i++)
                system.ObserveShot(transient, actor, Angle.Zero, "MuzzleFlashEffect");
            reused = ReferenceEquals(first, entities.GetComponent<PointLightComponent>(transient));
            entities.DeleteEntity(permanent);
            entities.DeleteEntity(transient);
        });
        Assert.That(preserved, Is.True);
        Assert.That(reused, Is.True);
    }
}
