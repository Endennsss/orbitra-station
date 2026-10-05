using Content.IntegrationTests.Fixtures;
using Content.Shared._Orbitra.ThermalVision;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Client.GameObjects;
using System.Numerics;
using ServerThermal = Content.Server._Orbitra.ThermalVision.OrbitraThermalVisionSystem;
using ClientThermal = Content.Client._Orbitra.ThermalVision.OrbitraThermalVisionSystem;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraThermalVisionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [Test]
    public void ExposureAdaptationIsFrameRateIndependent()
    {
        var full = Content.Client._Orbitra.ThermalVision.OrbitraThermalExposure.Adaptation(1f, false);
        var half = Content.Client._Orbitra.ThermalVision.OrbitraThermalExposure.Adaptation(0.5f, false);
        Assert.That(full.X, Is.EqualTo(1f - (1f - half.X) * (1f - half.X)).Within(0.00001f));
        Assert.That(full.Y, Is.EqualTo(1f - (1f - half.Y) * (1f - half.Y)).Within(0.00001f));
        Assert.That(full.X, Is.GreaterThan(full.Y), "Bright scenes should adapt faster than dark ones.");
        Assert.That(Content.Client._Orbitra.ThermalVision.OrbitraThermalExposure.Adaptation(-1f, false), Is.EqualTo(Vector2.Zero));
        Assert.That(Content.Client._Orbitra.ThermalVision.OrbitraThermalExposure.Adaptation(0f, true), Is.EqualTo(Vector2.One));
    }

    [Test]
    public async Task ContactsRespectBiologyContainmentAndMapWithoutRadius()
    {
        var map = await Pair.CreateTestMap();
        var otherMap = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<ServerThermal>();
            var wearer = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var target = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 4, 0));
            SEntMan.SpawnEntity("WallSolid", new EntityCoordinates(map.Grid, 2, 0));
            Assert.That(system.IsThermalTarget(target, wearer), Is.True, "Wall must not block heat.");
            Server.System<SharedVisibilitySystem>().AddLayer(target, 32768);
            Assert.That(system.IsThermalTarget(target, wearer), Is.False, "Hidden visibility layers must remain hidden.");
            Server.System<SharedVisibilitySystem>().RemoveLayer(target, 32768);
            Assert.That(system.IsThermalTarget(wearer, wearer), Is.False);
            Server.System<MobStateSystem>().ChangeMobState(target, MobState.Critical);
            Assert.That(system.IsThermalTarget(target, wearer), Is.True);
            Server.System<MobStateSystem>().ChangeMobState(target, MobState.Dead);
            Assert.That(system.IsThermalTarget(target, wearer), Is.False);
            var animal = SEntMan.SpawnEntity("MobMouse", map.GridCoords);
            Assert.That(system.IsThermalTarget(animal, wearer), Is.True);
            var ghost = SEntMan.SpawnEntity("MobObserver", map.GridCoords);
            Assert.That(system.IsThermalTarget(ghost, wearer), Is.False);
            var robot = SEntMan.SpawnEntity("BorgChassisSelectable", map.GridCoords);
            Assert.That(system.IsThermalTarget(robot, wearer), Is.False);
            var remote = SEntMan.SpawnEntity("MobHuman", otherMap.GridCoords);
            Assert.That(system.IsThermalTarget(remote, wearer), Is.False);
            var transform = Server.System<SharedTransformSystem>();
            var origin = transform.GetMapCoordinates(wearer);
            transform.SetMapCoordinates(animal, new MapCoordinates(origin.Position + new Vector2(8, 0), origin.MapId));
            Assert.That(system.IsThermalTarget(animal, wearer), Is.True, "Targets beyond the old radius must be detected.");
            transform.SetMapCoordinates(animal, new MapCoordinates(origin.Position + new Vector2(100, 0), origin.MapId));
            Assert.That(system.IsThermalTarget(animal, wearer), Is.True);
            transform.SetMapCoordinates(animal, new MapCoordinates(origin.Position + new Vector2(1000, 0), origin.MapId));
            Assert.That(system.IsThermalTarget(animal, wearer), Is.True, "Detection must have no device radius.");
            var containers = Server.System<SharedContainerSystem>();
            var box = SEntMan.SpawnEntity(null, map.GridCoords);
            var container = containers.EnsureContainer<Container>(box, "thermal-test");
            Assert.That(containers.Insert(animal, container), Is.True);
            Assert.That(system.IsThermalTarget(animal, wearer), Is.False);
        });
    }

    [Test]
    public async Task UnlimitedContactsDoNotExpandPvsAndRevokeDeadTargets()
    {
        var map = await Pair.CreateTestMap();
        var otherMap = await Pair.CreateTestMap();
        EntityUid far = default;
        NetEntity farNet = default;
        NetEntity remoteNet = default;
        bool equipped = false;
        bool toggled = false;
        await Server.WaitPost(() =>
        {
            Server.ResolveDependency<Robust.Shared.Configuration.IConfigurationManager>()
                .SetCVar(Robust.Shared.CVars.NetPVS, true);
            var wearer = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            far = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 1000, 0));
            var remote = SEntMan.SpawnEntity("MobHuman", otherMap.GridCoords);
            var goggles = SEntMan.SpawnEntity("OrbitraClothingEyesThermal", map.GridCoords);
            farNet = SEntMan.GetNetEntity(far);
            remoteNet = SEntMan.GetNetEntity(remote);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, wearer);
            equipped = Server.System<InventorySystem>().TryEquip(wearer, goggles, "eyes", force: true);
            toggled = Server.System<ServerThermal>().TryToggle((goggles,
                SEntMan.GetComponent<OrbitraThermalVisionComponent>(goggles)), wearer);
        });
        await Pair.RunTicksSync(15);
        await Client.WaitAssertion(() =>
        {
            Assert.That(equipped && toggled, Is.True);
            var thermal = Client.System<ClientThermal>();
            Assert.That(thermal.IsActive(), Is.True);
            Assert.That(Array.Exists(thermal.Contacts!.Contacts, contact => contact.Target == farNet), Is.True);
            Assert.That(Array.Exists(thermal.Contacts.Contacts, contact => contact.Target == remoteNet), Is.False);
            Assert.That(thermal.TryGetContactSprite(new OrbitraThermalContact(farNet), out _), Is.False,
                "Unlimited authorization must not load an out-of-PVS sprite.");
        });
        await Server.WaitPost(() => Server.System<MobStateSystem>().ChangeMobState(far, MobState.Dead));
        await Pair.RunTicksSync(15);
        await Client.WaitAssertion(() =>
        {
            var thermal = Client.System<ClientThermal>();
            Assert.That(thermal.IsActive(), Is.True);
            Assert.That(Array.Exists(thermal.Contacts!.Contacts, contact => contact.Target == farNet), Is.False);
        });
    }

    [Test]
    public async Task EquipmentAndViewerLifecycleRevokesContacts()
    {
        var map = await Pair.CreateTestMap();
        var session = Server.PlayerMan.GetSessionById(Client.Session!.UserId);
        EntityUid wearer = default;
        EntityUid other = default;
        EntityUid goggles = default;
        await Server.WaitAssertion(() =>
        {
            wearer = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            other = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 3, 0));
            goggles = SEntMan.SpawnEntity("OrbitraClothingEyesThermal", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(session, wearer);
            Assert.That(Server.System<ServerThermal>().TryToggle((goggles, SEntMan.GetComponent<OrbitraThermalVisionComponent>(goggles)), wearer), Is.False);
            Assert.That(Server.System<InventorySystem>().TryEquip(wearer, goggles, "eyes", force: true), Is.True);
            Assert.That(Server.System<ServerThermal>().TryToggle((goggles, SEntMan.GetComponent<OrbitraThermalVisionComponent>(goggles)), other), Is.False);
        });
        await Check(false);
        await Toggle();
        await Check(true);
        await Client.WaitAssertion(() =>
        {
            var system = Client.System<ClientThermal>();
            var contact = system.Contacts!.Contacts[0];
            Assert.That(system.TryGetContactSprite(contact, out var sprite), Is.True);
            var transform = Client.System<SharedTransformSystem>();
            var before = CEntMan.GetComponent<TransformComponent>(sprite).LocalPosition;
            try
            {
                // Между пакетами тепловизор должен использовать тот же живой спрайт, а не снимок позиции.
                transform.SetLocalPosition(sprite.Owner, before + new Vector2(0.5f, 0));
                Assert.That(system.TryGetContactSprite(contact, out var moved), Is.True);
                Assert.That(moved.Comp, Is.SameAs(sprite.Comp));
                Assert.That(CEntMan.GetComponent<TransformComponent>(moved).LocalPosition, Is.EqualTo(before + new Vector2(0.5f, 0)));
                transform.SetLocalPosition(sprite.Owner, before + new Vector2(20, 0));
                Assert.That(system.TryGetContactSprite(contact, out _), Is.True, "Live PVS sprites must not be clipped by a device radius.");
            }
            finally { transform.SetLocalPosition(sprite.Owner, before); }
        });
        await Toggle();
        await Check(false);
        await Toggle();
        await Check(true);
        await Server.WaitAssertion(() => Assert.That(Server.System<InventorySystem>().TryUnequip(wearer, "eyes", force: true), Is.True));
        await Check(false);
        await Server.WaitAssertion(() => Assert.That(Server.System<InventorySystem>().TryEquip(wearer, goggles, "eyes", force: true), Is.True));
        await Check(false);
        await Toggle();
        await Check(true);
        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, other));
        await Check(false);
        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, wearer));
        await Check(false);
        await Toggle();
        await Check(true);
        await Server.WaitPost(() => SEntMan.DeleteEntity(goggles));
        await Check(false);

        async Task Toggle() => await Server.WaitAssertion(() =>
        {
            var device = SEntMan.GetComponent<OrbitraThermalVisionComponent>(goggles);
            var expected = !device.Enabled;
            Assert.That(device.ActionEntity, Is.Not.Null);
            var action = device.ActionEntity!.Value;
            var component = SEntMan.GetComponent<ActionComponent>(action);
            Server.System<SharedActionsSystem>().PerformAction(wearer, (action, component));
            Assert.That(device.Enabled, Is.EqualTo(expected));
            Assert.That(component.Toggled, Is.EqualTo(expected));
        });

        async Task Check(bool active)
        {
            await Pair.RunTicksSync(15);
            await Client.WaitAssertion(() =>
            {
                var system = Client.System<ClientThermal>();
                Assert.That(system.IsActive(), Is.EqualTo(active));
                if (active)
                {
                    Assert.That(system.Contacts!.Contacts.Length, Is.EqualTo(1));
                    var contact = system.Contacts.Contacts[0];
                    Assert.That(system.TryGetContactSprite(contact, out var sprite), Is.True);
                    Assert.That(sprite.Comp, Is.SameAs(CEntMan.GetComponent<SpriteComponent>(CEntMan.GetEntity(contact.Target))));
                }
            });
        }
    }
}
