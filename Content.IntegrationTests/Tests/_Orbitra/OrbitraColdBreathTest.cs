using System.Numerics;
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Particles;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Orbitra.Particles;
using Content.Shared.Atmos;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Foldable;
using Content.Shared.Humanoid;
using Content.Client._Orbitra.Particles;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraColdBreathTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase("MobHuman", 0.234375f)]
    [TestCase("MobDwarf", 0.078125f)]
    [TestCase("MobReptilian", 0.234375f)]
    public async Task MouthUsesHeadLayerAndSpeciesDisplacement(string prototype, float mouthHeight)
    {
        var map = await Pair.CreateTestMap();
        NetEntity net = default;
        await Pair.Server.WaitPost(() =>
        {
            var uid = Pair.Server.EntMan.SpawnEntity(prototype, map.GridCoords);
            net = Pair.Server.EntMan.GetNetEntity(uid);
            Pair.Server.PlayerMan.SetAttachedEntity(Pair.Server.PlayerMan.GetSessionById(Pair.Client.Session!.UserId), uid);
        });
        await Pair.RunTicksSync(20);
        await Pair.Client.WaitAssertion(() =>
        {
            var em = Pair.Client.EntMan;
            var uid = em.GetEntity(net);
            var sprite = em.GetComponent<SpriteComponent>(uid);
            var system = Pair.Client.System<OrbitraParticleSystem>();
            var sprites = Pair.Client.System<SpriteSystem>();
            var transforms = Pair.Client.System<SharedTransformSystem>();
            var visual = em.GetComponent<OrbitraColdBreathVisualsComponent>(uid);
            Assert.That(visual.South.Y, Is.EqualTo(mouthHeight));
            Assert.That(sprites.TryGetLayer((uid, sprite), HumanoidVisualLayers.Head, out var head, false), Is.True);
            Assert.That(system.TryGetBreathLayer((uid, sprite), true, out var selected), Is.True);
            Assert.That(selected, Is.SameAs(head));
            Assert.That(selected!.CopyToShaderParameters, Is.Null);
            // Даже смещённая грудь не должна управлять точкой выдоха.
            sprites.LayerSetOffset((uid, sprite), HumanoidVisualLayers.Chest, new Vector2(2, 2));
            system.PreviewQuality("Medium");
            foreach (var rotation in new[] { Angle.Zero, Angle.FromDegrees(90) })
            {
                sprites.SetRotation((uid, sprite), rotation);
                system.Pool.Clear();
                Assert.That(system.TryExhale((uid, sprite), 15, Angle.Zero), Is.True);
                ref var particle = ref system.Pool.Particles[0];
                var actual = transforms.ToMapCoordinates(new EntityCoordinates(particle.Parent, particle.Origin)).Position;
                head!.GetLayerDrawMatrix(Robust.Shared.Graphics.RSI.RsiDirection.South, out var local);
                var expected = Vector2.Transform(visual.South, local * sprite.LocalMatrix *
                    Matrix3x2.CreateTranslation(transforms.GetWorldPosition(uid)));
                Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.0001f));
            }
        });
    }

    [Test]
    public async Task NetworkStateAndWorldAnchoredPuffs()
    {
        var map = await Pair.CreateTestMap();
        NetEntity net = default;
        await Pair.Server.WaitPost(() =>
        {
            var air = new GasMixture(2500) { Temperature = 258.15f };
            air.AdjustMoles(Gas.Oxygen, 22);
            air.AdjustMoles(Gas.Nitrogen, 82);
            Pair.Server.System<AtmosphereSystem>().SetMapAtmosphere(map.MapUid, false, air);
            var uid = Pair.Server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.MapUid, new Vector2(10, 10)));
            net = Pair.Server.EntMan.GetNetEntity(uid);
            Pair.Server.PlayerMan.SetAttachedEntity(Pair.Server.PlayerMan.GetSessionById(Pair.Client.Session!.UserId), uid);
        });
        await Pair.RunTicksSync(90);
        byte level = 0;
        var emitted = false;
        var anchored = false;
        var count = 0;
        var expired = false;
        var bounded = false;
        await Pair.Client.WaitPost(() =>
        {
            var em = Pair.Client.EntMan;
            var uid = em.GetEntity(net);
            level = em.GetComponent<OrbitraColdBreathComponent>(uid).Intensity;
            var particles = Pair.Client.System<OrbitraParticleSystem>();
            particles.PreviewQuality("Medium");
            particles.Pool.Clear();
            emitted = particles.TryExhale((uid, em.GetComponent<SpriteComponent>(uid)), level, Angle.Zero);
            count = particles.Pool.Count;
            if (count > 0)
            {
                var parent = particles.Pool.Particles[0].Parent;
                anchored = parent == map.CMapUid && parent != uid;
            }
            for (var i = 0; i < 1000; i++)
                particles.TryExhale((uid, em.GetComponent<SpriteComponent>(uid)), level, Angle.Zero);
            bounded = particles.Pool.Count == 384;
            particles.Pool.Update(1f);
            expired = particles.Pool.Count == 0;
            particles.PreviewQuality("Off");
            emitted &= !particles.TryExhale((uid, em.GetComponent<SpriteComponent>(uid)), level, Angle.Zero);
        });
        Assert.Multiple(() =>
        {
            Assert.That(level, Is.EqualTo(15));
            Assert.That(emitted, Is.True);
            Assert.That(count, Is.InRange(2, 3));
            Assert.That(anchored, Is.True);
            Assert.That(bounded, Is.True);
            Assert.That(expired, Is.True);
        });
    }

    [TestCase("MobHuman")]
    [TestCase("MobDwarf")]
    [TestCase("MobReptilian")]
    [TestCase("MobCat")]
    [TestCase("MobMouse")]
    public async Task AmbientAndRespirationEligibility(string prototype)
    {
        var map = await Pair.CreateTestMap();
        var result = new List<byte>();
        await Pair.Server.WaitPost(() =>
        {
            var em = Pair.Server.EntMan;
            var uid = em.SpawnEntity(prototype, new EntityCoordinates(map.MapUid, new Vector2(10, 10)));
            var resp = em.GetComponent<RespiratorComponent>(uid);
            var transform = em.GetComponent<TransformComponent>(uid);
            var system = Pair.Server.System<OrbitraColdBreathSystem>();
            var atmos = Pair.Server.System<AtmosphereSystem>();
            var air = new GasMixture(2500) { Temperature = 258.15f };
            air.AdjustMoles(Gas.Oxygen, 22);
            air.AdjustMoles(Gas.Nitrogen, 82);
            atmos.SetMapAtmosphere(map.MapUid, false, air);
            result.Add(system.GetIntensity((uid, resp), transform));
            air.Temperature = 293.15f;
            atmos.SetMapAtmosphere(map.MapUid, false, air);
            result.Add(system.GetIntensity((uid, resp), transform));
            air.Temperature = 258.15f;
            atmos.SetMapAtmosphere(map.MapUid, false, air);
            Pair.Server.System<RespiratorSystem>().UpdateSaturation(uid, -100, resp);
            result.Add(system.GetIntensity((uid, resp), transform));
            Pair.Server.System<RespiratorSystem>().UpdateSaturation(uid, 100, resp);
            Pair.Server.System<MobStateSystem>().ChangeMobState(uid, MobState.Critical);
            result.Add(system.GetIntensity((uid, resp), transform));
            Pair.Server.System<MobStateSystem>().ChangeMobState(uid, MobState.Dead);
            result.Add(system.GetIntensity((uid, resp), transform));
            Pair.Server.System<MobStateSystem>().ChangeMobState(uid, MobState.Alive);
            air.Clear();
            atmos.SetMapAtmosphere(map.MapUid, true, air);
            result.Add(system.GetIntensity((uid, resp), transform));
        });
        Assert.That(result, Is.EqualTo(new byte[] { 15, 0, 0, 0, 0, 0 }));
    }

    [Test]
    public async Task OnlyEquippedActiveBlockersCoverMouth()
    {
        var map = await Pair.CreateTestMap();
        var result = new List<bool>();
        await Pair.Server.WaitPost(() =>
        {
            var em = Pair.Server.EntMan;
            var uid = em.SpawnEntity("MobHuman", map.GridCoords);
            var mask = em.SpawnEntity("ClothingMaskGas", map.GridCoords);
            var head = em.SpawnEntity("ClothingHeadHatWelding", map.GridCoords);
            var check = Pair.Server.System<SharedOrbitraColdBreathSystem>();
            var inventory = Pair.Server.System<InventorySystem>();
            result.Add(check.IsMouthCovered(uid));
            inventory.TryEquip(uid, mask, "mask", silent: true, force: true);
            result.Add(check.IsMouthCovered(uid));
            // Проверяем штатный контракт FoldedEvent, которым переключается IngestionBlocker.
            var open = new FoldedEvent(true, uid);
            em.EventBus.RaiseLocalEvent(mask, ref open);
            result.Add(check.IsMouthCovered(uid));
            em.EnsureComponent<IngestionBlockerComponent>(head);
            inventory.TryEquip(uid, head, "head", silent: true, force: true);
            result.Add(check.IsMouthCovered(uid));
            em.EventBus.RaiseLocalEvent(head, ref open);
            result.Add(check.IsMouthCovered(uid));
            var closed = new FoldedEvent(false, uid);
            em.EventBus.RaiseLocalEvent(mask, ref closed);
            result.Add(check.IsMouthCovered(uid));
        });
        Assert.That(result, Is.EqualTo(new[] { false, true, false, true, false, true }));
    }
}
