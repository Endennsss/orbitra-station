using Content.Client._Lime.NightVision;
using Content.Client.Overlays;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Inventory;
using Content.Shared.NightVision;
using Content.Shared.Overlays;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lime;

[TestFixture]
public sealed class LimeNightVisionTest : GameTest
{
    // Тест меняет управляемую сущность сессии.
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task EquipmentToggleAndViewerChangesSelectCorrectOverlay()
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var session = server.PlayerMan.GetSessionById(client.Session!.UserId);
        EntityUid wearer = default;
        EntityUid other = default;
        EntityUid goggles = default;
        var equipped = false;

        await server.WaitPost(() =>
        {
            wearer = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            other = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            goggles = server.EntMan.SpawnEntity("LimeClothingEyesNightVision", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, wearer);
            equipped = server.System<InventorySystem>().TryEquip(wearer, goggles, "eyes", force: true);
        });
        Assert.That(equipped, Is.True);
        await AssertOverlays(false, false);

        await server.WaitPost(() => server.System<SharedNightVisionSystem>().SetEnabled(goggles, true, wearer));
        await AssertOverlays(true, false);

        await server.WaitPost(() => server.System<SharedNightVisionSystem>().SetEnabled(goggles, false, wearer));
        await AssertOverlays(false, false);

        await server.WaitPost(() => server.System<SharedNightVisionSystem>().SetEnabled(goggles, true, wearer));
        await AssertOverlays(true, false);

        await server.WaitPost(() => server.System<InventorySystem>().TryUnequip(wearer, "eyes", force: true));
        await AssertOverlays(false, false);

        await server.WaitPost(() => server.System<InventorySystem>().TryEquip(wearer, goggles, "eyes", force: true));
        await AssertOverlays(true, false);

        await server.WaitPost(() => server.PlayerMan.SetAttachedEntity(session, other));
        await AssertOverlays(false, false);

        await server.WaitPost(() =>
        {
            var natural = server.EntMan.AddComponent<NightVisionComponent>(other);
            natural.Prioritized = false;
            server.EntMan.Dirty(other, natural);
        });
        await AssertOverlays(false, true);

        await server.WaitPost(() =>
        {
            var inventory = server.System<InventorySystem>();
            inventory.TryUnequip(wearer, "eyes", force: true);
            inventory.TryEquip(other, goggles, "eyes", force: true);
        });
        await AssertOverlays(true, false);
        await server.WaitPost(() => server.System<InventorySystem>().TryUnequip(other, "eyes", force: true));
        await AssertOverlays(false, true);

        await server.WaitPost(() => server.PlayerMan.SetAttachedEntity(session, null));
        await AssertOverlays(false, false);

        async Task AssertOverlays(bool device, bool natural)
        {
            // Экипировка, состояние предмета и смена наблюдателя должны дойти до клиента.
            await pair.RunTicksSync(10);
            await client.WaitAssertion(() =>
            {
                var overlays = client.ResolveDependency<IOverlayManager>();
                Assert.That(overlays.HasOverlay<LimeNightVisionOverlay>(), Is.EqualTo(device));
                Assert.That(overlays.HasOverlay<LimeNightVisionLightOverlay>(), Is.EqualTo(device));
                Assert.That(overlays.HasOverlay<NightVisionOverlay>(), Is.EqualTo(natural));
            });
        }
    }
}
