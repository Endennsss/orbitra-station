using System.Numerics;
using Content.Client._Orbitra.Particles;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Weapons.Melee;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Server.Power.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraParticleNetworkTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task InertSpriteDoesNotRequireAppearance(bool doAfterOnly)
    {
        var client = Pair.Client;
        var map = await Pair.CreateTestMap();
        NetEntity netEntity = default;
        await Pair.Server.WaitPost(() =>
        {
            var actor = Pair.Server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            var entity = Pair.Server.EntMan.SpawnEntity("Catwalk", map.GridCoords);
            netEntity = Pair.Server.EntMan.GetNetEntity(entity);
            Pair.Server.PlayerMan.SetAttachedEntity(Pair.Server.PlayerMan.GetSessionById(client.Session!.UserId), actor);
        });
        await Pair.RunTicksSync(20);
        await client.WaitPost(() =>
        {
            var entity = client.EntMan.GetEntity(netEntity);
            client.EntMan.RemoveComponent<AppearanceComponent>(entity);
            if (doAfterOnly)
                client.EntMan.EnsureComponent<Content.Shared.DoAfter.DoAfterComponent>(entity);
            var particles = client.System<OrbitraParticleSystem>();
            particles.PreviewQuality("Medium");
            particles.Pool.Clear();
            // В тестовом хосте запись ошибки превращает этот регрессионный тест в падение.
            for (var i = 0; i < 1000; i++)
                particles.Collect(entity);
        });
        await client.WaitAssertion(() => Assert.That(client.System<OrbitraParticleSystem>().Pool.Count, Is.Zero));
    }

    [Test]
    public async Task BurningBodyEmitsUntilAppearanceIsExtinguished()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var map = await Pair.CreateTestMap();
        EntityUid body = default;
        NetEntity netBody = default;
        await server.WaitPost(() =>
        {
            body = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            // Проверяем визуальный контракт отдельно от атмосферы пустой тестовой карты.
            server.EntMan.RemoveComponent<FlammableComponent>(body);
            netBody = server.EntMan.GetNetEntity(body);
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.GetSessionById(client.Session!.UserId), body);
        });
        await Pair.RunTicksSync(20);
        await server.WaitPost(() =>
        {
            var appearance = server.System<SharedAppearanceSystem>();
            appearance.SetData(body, FireVisuals.OnFire, true);
            appearance.SetData(body, FireVisuals.FireStacks, 3f);
        });
        await Pair.RunTicksSync(4);
        await client.WaitPost(() => client.System<OrbitraParticleSystem>().PreviewQuality("Medium"));
        for (var i = 0; i < 30; i++)
        {
            // Эмиттер использует реальное время кадра, а не ускоренные игровые тики теста.
            await Task.Delay(20);
            await client.WaitPost(() => client.System<OrbitraParticleSystem>().Collect(client.EntMan.GetEntity(netBody)));
            await Pair.RunTicksSync(1);
        }
        var count = 0;
        await client.WaitPost(() => count = client.System<OrbitraParticleSystem>().Pool.Count);
        Assert.That(count, Is.GreaterThan(0), "Burning humanoid must emit cosmetic particles.");
        await server.WaitPost(() => server.System<SharedAppearanceSystem>().SetData(body, FireVisuals.OnFire, false));
        await Pair.RunTicksSync(4);
        await client.WaitPost(() => client.System<OrbitraParticleSystem>().Pool.Clear());
        for (var i = 0; i < 10; i++)
        {
            await client.WaitPost(() => client.System<OrbitraParticleSystem>().Collect(client.EntMan.GetEntity(netBody)));
            await Pair.RunTicksSync(1);
        }
        await client.WaitPost(() => count = client.System<OrbitraParticleSystem>().Pool.Count);
        Assert.That(count, Is.Zero);
    }

    [TestCase("MobHuman")]
    [TestCase("MobDwarf")]
    [TestCase("MobReptilian")]
    public async Task MeleeHitOnBodyEmitsBlood(string prototype)
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var map = await Pair.CreateTestMap();
        var session = server.PlayerMan.GetSessionById(client.Session!.UserId);
        EntityUid actor = default;
        EntityUid target = default;
        await server.WaitPost(() =>
        {
            actor = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            target = server.EntMan.SpawnEntity(prototype, new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX));
            server.PlayerMan.SetAttachedEntity(session, actor);
            server.System<SharedCombatModeSystem>().SetInCombatMode(actor, true);
        });
        await Pair.RunTicksSync(20);
        await client.WaitPost(() =>
        {
            client.System<OrbitraParticleSystem>().PreviewQuality("Medium");
            client.System<OrbitraParticleSystem>().Pool.Clear();
        });
        await server.WaitPost(() => server.System<SharedMeleeWeaponSystem>().AttemptLightAttack(actor, actor,
            server.EntMan.GetComponent<MeleeWeaponComponent>(actor), target));
        await Pair.RunTicksSync(4);
        var count = 0;
        await client.WaitPost(() =>
        {
            var pool = client.System<OrbitraParticleSystem>().Pool;
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool.Particles[i].Effect.ID == "OrbitraParticleBlood" && pool.Particles[i].Tint != null)
                    count++;
            }
        });
        Assert.That(count, Is.EqualTo(8));
    }

    [Test]
    public async Task ConfirmedHitReachesClientOnceAndMissDoesNotEmit()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var map = await Pair.CreateTestMap();
        var session = server.PlayerMan.GetSessionById(client.Session!.UserId);
        EntityUid actor = default;
        EntityUid wall = default;
        await server.WaitPost(() =>
        {
            actor = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            wall = server.EntMan.SpawnEntity("WallSolid", new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX));
            server.PlayerMan.SetAttachedEntity(session, actor);
            server.System<SharedCombatModeSystem>().SetInCombatMode(actor, true);
        });
        await Pair.RunTicksSync(20);
        await client.WaitPost(() =>
        {
            var particles = client.System<OrbitraParticleSystem>();
            particles.PreviewQuality("Medium");
            particles.Pool.Clear();
        });
        var hit = false;
        await server.WaitPost(() =>
        {
            hit = server.System<SharedMeleeWeaponSystem>().AttemptLightAttack(actor, actor,
                server.EntMan.GetComponent<MeleeWeaponComponent>(actor), wall);
        });
        Assert.That(hit, Is.True);
        await Pair.RunTicksSync(4);
        // Кулак не пробивает flat reduction стальной стены: нулевой урон не создаёт косметику.
        await client.WaitAssertion(() => Assert.That(client.System<OrbitraParticleSystem>().Pool.Count, Is.Zero));
        await Pair.RunTicksSync(40);
        await server.WaitPost(() =>
        {
            var weapon = server.EntMan.GetComponent<MeleeWeaponComponent>(actor);
            weapon.Damage = new DamageSpecifier { DamageDict = { ["Blunt"] = 25 } };
            server.EntMan.Dirty(actor, weapon);
            server.System<SharedMeleeWeaponSystem>().AttemptLightAttack(actor, actor, weapon, wall);
        });
        await Pair.RunTicksSync(4);
        await client.WaitAssertion(() => Assert.That(client.System<OrbitraParticleSystem>().Pool.Count, Is.EqualTo(8)));

        await Pair.RunTicksSync(40);
        await client.WaitPost(() => client.System<OrbitraParticleSystem>().Pool.Clear());
        await server.WaitPost(() => server.System<SharedMeleeWeaponSystem>().AttemptLightAttackMiss(actor, actor,
            server.EntMan.GetComponent<MeleeWeaponComponent>(actor), map.GridCoords));
        await Pair.RunTicksSync(4);
        await client.WaitAssertion(() => Assert.That(client.System<OrbitraParticleSystem>().Pool.Count, Is.Zero));
    }

    [TestCase(false, 0, false)]
    [TestCase(true, 16, false)]
    [TestCase(false, 0, true)]
    [TestCase(true, 16, true)]
    public async Task ElectricalBreakUsesPowerBeforeBreakage(bool powered, int count, bool damage)
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var map = await Pair.CreateTestMap();
        var session = server.PlayerMan.GetSessionById(client.Session!.UserId);
        EntityUid machine = default;
        await server.WaitPost(() =>
        {
            var actor = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            machine = server.EntMan.SpawnEntity("ComputerComms",
                new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX));
            server.PlayerMan.SetAttachedEntity(session, actor);
        });
        await Pair.RunTicksSync(20);
        await client.WaitPost(() =>
        {
            client.System<OrbitraParticleSystem>().PreviewQuality("Medium");
            client.System<OrbitraParticleSystem>().Pool.Clear();
        });
        await server.WaitPost(() =>
        {
            // Задаём снимок результата электросети непосредственно перед проверяемым событием.
            var power = server.EntMan.GetComponent<ApcPowerReceiverComponent>(machine);
            power.Powered = powered;
            power.PowerDisabled = false;
            power.NetworkLoad.ReceivingPower = powered ? power.Load : 0;
            if (damage)
                server.System<DamageableSystem>().TryChangeDamage(machine,
                    new DamageSpecifier { DamageDict = { ["Blunt"] = 100 } }, ignoreResistances: true);
            else
                server.System<SharedDestructibleSystem>().BreakEntity(machine);
        });
        await Pair.RunTicksSync(4);
        var actual = 0;
        await client.WaitPost(() => actual = client.System<OrbitraParticleSystem>().Pool.Count);
        TestContext.Out.WriteLine($"Electrical burst: powered={powered}, damage={damage}, particles={actual}, expected={count}");
        Assert.That(actual, Is.EqualTo(count));
    }
}
