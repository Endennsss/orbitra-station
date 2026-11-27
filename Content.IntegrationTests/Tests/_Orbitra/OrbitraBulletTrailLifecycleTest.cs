using System.Numerics;
using Content.Client._Orbitra.Particles;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Orbitra.Particles;
using Content.Shared.CCVar;
using Robust.Client.GameObjects;
using Robust.Client.GameStates;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraBulletTrailLifecycleTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public Task OffClearsActiveAndFadingTrailsAndResumesAtCurrentPosition()
    {
        return OnClientMap((particles, origin) =>
        {
            var flying = SpawnMovingBullet(particles, origin);
            var ending = SpawnMovingBullet(particles, origin.Offset(Vector2.UnitY));
            CEntMan.DeleteEntity(ending);
            Assert.That(particles.Flying.ContainsKey(flying), Is.True);
            Assert.That(particles.Fading, Has.Count.EqualTo(1));

            particles.PreviewQuality("Off");
            Assert.That(particles.Flying, Is.Empty);
            Assert.That(particles.Fading, Is.Empty);
            var next = origin.Offset(Vector2.UnitX);
            Client.System<SharedTransformSystem>().SetCoordinates(flying, next);
            particles.FrameUpdate(0f);
            Assert.That(particles.Flying, Is.Empty);

            particles.PreviewQuality("Medium");
            particles.FrameUpdate(0f);
            var path = particles.Flying[flying].Path;
            Assert.That(path.Count, Is.EqualTo(1));
            Assert.That(path.GetPoint(0), Is.EqualTo(next.Position));
        });
    }

    [Test]
    public Task DeletionFadesForAtMostEightyMillisecondsUnlessMotionIsReduced()
    {
        return OnClientMap((particles, origin) =>
        {
            var bullet = SpawnMovingBullet(particles, origin);
            // При сетевом удалении клиент сначала отсоединяет сущность от родителя.
            Client.System<SharedTransformSystem>().DetachEntity(bullet, CComp<TransformComponent>(bullet));
            CEntMan.DeleteEntity(bullet);
            Assert.That(particles.Flying.ContainsKey(bullet), Is.False);
            Assert.That(particles.Fading, Has.Count.EqualTo(1));
            Assert.That(particles.Fading[0].Path.Length, Is.Positive);

            // Устанавливаем возраст хвоста на границу интервала без ожидания реального кадра.
            particles.Fading[0].Ended = Client.Timing.RealTime - TimeSpan.FromMilliseconds(80);
            particles.FrameUpdate(0f);
            Assert.That(particles.Fading, Is.Empty);

            Client.CfgMan.SetCVar(CCVars.ReducedMotion, true);
            var reduced = SpawnMovingBullet(particles, origin);
            Assert.That(particles.Flying[reduced].Path.Length, Is.Positive);
            CEntMan.DeleteEntity(reduced);
            Assert.That(particles.Fading, Is.Empty);
            Assert.That(particles.Flying.ContainsKey(reduced), Is.False);
        });
    }

    [Test]
    public Task LeavingPvsForgetsTheTrailBeforeDeletion()
    {
        return OnClientMap((particles, origin) =>
        {
            var bullet = SpawnMovingBullet(particles, origin);
            Client.Resolve<IClientGameStateManager>().DetachImmediate([CEntMan.GetNetEntity(bullet)]);
            Assert.That(CComp<MetaDataComponent>(bullet).Flags.HasFlag(MetaDataFlags.Detached), Is.True);
            Assert.That(particles.Flying.ContainsKey(bullet), Is.False);
            CEntMan.DeleteEntity(bullet);
            Assert.That(particles.Fading, Is.Empty);
        });
    }

    [Test]
    public Task PausedAndInvisibleBulletsReleaseTheirTrail()
    {
        return OnClientMap((particles, origin) =>
        {
            var paused = SpawnMovingBullet(particles, origin);
            var hidden = SpawnMovingBullet(particles, origin.Offset(Vector2.UnitY));
            var metadata = Client.System<MetaDataSystem>();
            metadata.SetEntityPaused(paused, true);
            CEntMan.RemoveComponent<SpriteComponent>(hidden);
            particles.FrameUpdate(0f);
            Assert.That(particles.Flying.ContainsKey(paused), Is.False);
            Assert.That(particles.Flying.ContainsKey(hidden), Is.False);
            Assert.That(particles.Fading, Is.Empty);

            metadata.SetEntityPaused(paused, false);
            particles.FrameUpdate(0f);
            Assert.That(particles.Flying[paused].Path.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public Task LowQualitySharesItsBudgetBetweenFlyingAndFadingBullets()
    {
        return OnClientMap((particles, origin) =>
        {
            particles.PreviewQuality("Low");
            var fading = SpawnMovingBullet(particles, origin);
            CEntMan.DeleteEntity(fading);
            for (var i = 0; i < 36; i++)
                CSpawnAtPosition("BulletPistol", origin);

            // Фиксируем начало затухания после спавна всей очереди.
            particles.Fading[0].Ended = Client.Timing.RealTime;
            particles.FrameUpdate(0f);
            Assert.That(particles.Flying, Has.Count.EqualTo(31));
            Assert.That(particles.Fading, Has.Count.EqualTo(1));
            particles.Fading[0].Ended -= TimeSpan.FromMilliseconds(80);
            particles.FrameUpdate(0f);
            Assert.That(particles.Fading, Is.Empty);
            Assert.That(particles.Flying, Has.Count.EqualTo(32));
        });
    }

    [Test]
    public Task ReparentingAndMapChangesStartNewPaths()
    {
        return OnClientMap((particles, origin) =>
        {
            var bullet = SpawnMovingBullet(particles, origin);
            var parent = CSpawnAtPosition(null, origin);
            var next = new EntityCoordinates(parent, 0.3f, 0.1f);
            var transform = Client.System<SharedTransformSystem>();
            transform.SetCoordinates(bullet, next);
            particles.FrameUpdate(0f);
            var trail = particles.Flying[bullet];
            Assert.That(trail.Parent, Is.EqualTo(parent));
            Assert.That(trail.Path.Count, Is.EqualTo(1));
            Assert.That(trail.Path.GetPoint(0), Is.EqualTo(next.Position));
            transform.SetCoordinates(bullet, next.Offset(Vector2.UnitY / 4));
            particles.FrameUpdate(0f);
            Assert.That(trail.Path.Length, Is.Positive);

            var otherMap = Client.System<SharedMapSystem>().CreateMap();
            try
            {
                transform.SetCoordinates(bullet, new EntityCoordinates(otherMap, 0.3f, 0.1f));
                particles.FrameUpdate(0f);
                Assert.That(particles.Flying.ContainsKey(bullet), Is.False);
                Assert.That(particles.Fading, Is.Empty);

                transform.SetCoordinates(bullet, next);
                particles.FrameUpdate(0f);
                var returned = particles.Flying[bullet].Path;
                Assert.That(returned.Count, Is.EqualTo(1));
                Assert.That(returned.GetPoint(0), Is.EqualTo(next.Position));

                transform.SetCoordinates(bullet, next.Offset(Vector2.UnitX / 4));
                particles.FrameUpdate(0f);
                Assert.That(returned.Length, Is.Positive);
                transform.SetCoordinates(bullet, origin);
                CEntMan.DeleteEntity(bullet);
                Assert.That(particles.Fading, Is.Empty, "Reparenting before deletion must discard the old path.");
            }
            finally
            {
                CEntMan.DeleteEntity(otherMap);
            }
        });
    }

    private EntityUid SpawnMovingBullet(OrbitraParticleSystem particles, EntityCoordinates origin)
    {
        var bullet = CSpawnAtPosition("BulletPistol", origin);
        particles.FrameUpdate(0f);
        // За краем тестового тайла движок привязывает пулю к карте при спавне.
        var spawned = CComp<TransformComponent>(bullet).Coordinates;
        Client.System<SharedTransformSystem>().SetCoordinates(bullet, spawned.Offset(Vector2.UnitX / 4));
        particles.FrameUpdate(0f);
        Assert.That(particles.Flying[bullet].Path.Count, Is.EqualTo(2));
        return bullet;
    }

    private async Task OnClientMap(Action<OrbitraParticleSystem, EntityCoordinates> check)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            var viewer = SSpawnAtPosition("MobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(Pair.Player, viewer);
        });
        await Pair.RunTicksSync(20);
        await Client.WaitAssertion(() =>
        {
            var particles = Client.System<OrbitraParticleSystem>();
            var quality = Client.CfgMan.GetCVar(OrbitraParticleCVars.Quality);
            var reduced = Client.CfgMan.GetCVar(CCVars.ReducedMotion);
            try
            {
                Client.CfgMan.SetCVar(CCVars.ReducedMotion, false);
                particles.PreviewQuality("Medium");
                check(particles, CComp<TransformComponent>(Client.AttachedEntity!.Value).Coordinates);
            }
            finally
            {
                particles.PreviewQuality(quality);
                Client.CfgMan.SetCVar(CCVars.ReducedMotion, reduced);
            }
        });
    }
}
