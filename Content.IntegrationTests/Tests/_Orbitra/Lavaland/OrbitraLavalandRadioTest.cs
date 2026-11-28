using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Lavaland;
using Content.Shared._Orbitra.Lavaland;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra.Lavaland;

public sealed class OrbitraLavalandRadioTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task FerryWarningsReachCargoHeadsetsOnBothMapsOnce()
    {
        var home = await Pair.CreateTestMap();
        var session = ServerSession!;
        var previous = session.AttachedEntity;
        var historyStart = 0;
        var departure = string.Empty;
        var sender = string.Empty;
        var channel = string.Empty;
        const string StationUplink = "Outpost to station";
        const string OutpostDownlink = "Station to outpost";
        const string SameMap = "Both terminals on station";
        const string Passenger = "Passenger headset";
        try
        {
            await Client.WaitPost(() => historyStart = Client.Resolve<IUserInterfaceManager>().GetUIController<ChatUIController>().History.Count);
            await Server.WaitAssertion(() =>
            {
                var maps = Server.System<SharedMapSystem>();
                var xform = Server.System<SharedTransformSystem>();
                var inventory = Server.System<InventorySystem>();
                var ferry = Server.System<OrbitraLavalandFerrySystem>();
                var radio = Server.System<OrbitraLavalandRadioSystem>();
                var loc = Server.Resolve<ILocalizationManager>();
                var dockCoords = new EntityCoordinates(home.Grid, .5f, .5f);
                var terminalCoords = new EntityCoordinates(home.Grid, .5f, 1.5f);
                maps.SetTile(home.Grid, new Vector2i(0, 1), new Tile(1));
                SEntMan.SpawnAttachedTo("OrbitraLavalandStationDock", dockCoords);
                var station = SEntMan.SpawnAttachedTo("OrbitraLavalandStationTerminal", terminalCoords);
                var listener = SSpawnAtPosition("MobHuman", terminalCoords);
                Server.PlayerMan.SetAttachedEntity(session, listener);
                var headset = SSpawnAtPosition("ClothingHeadsetCargo", terminalCoords);
                Assert.That(inventory.TryEquip(listener, headset, "ears", force: true), Is.True);
                Assert.That(ferry.TryInitializeStation(home.Grid), Is.True);
                ferry.Request(station, listener);
                Assert.That(ferry.TryGetRoute(out var route), Is.True);
                Assert.That(route!.Status, Is.EqualTo(LavalandFerryStatus.Boarding));
                var outpost = route.OutpostTerminal!.Value;
                var outpostCoords = SComp<TransformComponent>(outpost).Coordinates;
                Assert.That(SComp<TransformComponent>(outpost).MapID, Is.Not.EqualTo(home.MapId));
                departure = loc.GetString("orbitra-lavaland-warning-station", ("seconds", 10));
                sender = SComp<MetaDataComponent>(station).EntityName;
                channel = loc.GetString("chat-radio-supply");

                radio.Broadcast(outpost, StationUplink);
                xform.SetCoordinates(listener, outpostCoords);
                radio.Broadcast(station, OutpostDownlink);

                xform.SetCoordinates(listener, terminalCoords);
                xform.Unanchor(outpost);
                xform.SetCoordinates(outpost, terminalCoords);
                Assert.That(xform.AnchorEntity(outpost), Is.True);
                radio.Broadcast(station, SameMap);

                Assert.That(inventory.TryUnequip(listener, "ears", force: true), Is.True);
                var grey = SSpawnAtPosition("ClothingHeadsetGrey", terminalCoords);
                Assert.That(inventory.TryEquip(listener, grey, "ears", force: true), Is.True);
                radio.Broadcast(station, Passenger);
                Assert.That(ferry.GetState(station).Status, Is.EqualTo(LavalandFerryStatus.Boarding));
            });
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() =>
            {
                var history = Client.Resolve<IUserInterfaceManager>().GetUIController<ChatUIController>().History;
                var messages = history.Skip(historyStart).Select(entry => entry.Msg).ToArray();
                foreach (var text in new[] { departure, StationUplink, OutpostDownlink, SameMap })
                {
                    var received = messages.Where(msg => msg.Message == text).ToArray();
                    Assert.That(received, Has.Length.EqualTo(1), text);
                    Assert.That(received[0].Channel, Is.EqualTo(ChatChannel.Radio));
                    Assert.That(received[0].WrappedMessage, Does.Contain(sender).And.Contain(channel));
                }
                Assert.That(messages.Any(msg => msg.Message == Passenger), Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, previous);
                // Маршрут и поверхность живут до конца смены, дольше тестовой карты.
                SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            });
            await Pair.RunTicksSync(2);
        }
    }
}
