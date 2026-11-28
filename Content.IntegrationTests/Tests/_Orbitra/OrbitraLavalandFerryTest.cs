using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Lavaland;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Orbitra.Lavaland;
using Content.Shared.Cargo.Components;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Timing;
using Content.Shared.Vehicle.Systems;
using Robust.Client.Input;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraLavalandFerryTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task FerryGeneratorsRestoreLightsAndRadarWithoutShorePower()
    {
        var map = await Pair.CreateTestMap();
        var batteries = SEntMan.System<SharedBatterySystem>();
        var devices = new List<EntityUid>();
        await Server.WaitAssertion(() =>
        {
            var loader = SEntMan.System<MapLoaderSystem>();
            Assert.That(loader.TryLoadGrid(map.MapId, new ResPath("/Maps/_Orbitra/Lavaland/mining.yml"),
                out var ferry, offset: new Vector2(30, 30)), Is.True);
            var children = SEntMan.GetComponent<TransformComponent>(ferry!.Value).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                devices.Add(child);
                if (SEntMan.HasComponent<PowerNetworkBatteryComponent>(child))
                    batteries.SetCharge(child, 0);
            }
        });
        await Pair.RunSeconds(20);
        await Server.WaitAssertion(() =>
        {
            var apcs = devices.Where(SEntMan.HasComponent<ApcComponent>).ToArray();
            Assert.That(apcs, Is.Not.Empty);
            foreach (var apc in apcs)
                Assert.That(batteries.GetCharge(apc).Charge, Is.Positive);
            var lights = devices.Where(SEntMan.HasComponent<PoweredLightComponent>).ToArray();
            Assert.That(lights, Is.Not.Empty);
            foreach (var light in lights)
                Assert.That(SEntMan.GetComponent<PoweredLightComponent>(light).CurrentLit, Is.True);
            var radar = devices.Single(uid => SEntMan.HasComponent<RadarConsoleComponent>(uid) &&
                                              SEntMan.HasComponent<ApcPowerReceiverComponent>(uid));
            Assert.That(SEntMan.System<SharedPowerReceiverSystem>().IsPowered(radar), Is.True);
            Assert.That(devices.Any(SEntMan.HasComponent<CargoOrderConsoleComponent>), Is.False);
        });
    }

    [Test]
    public async Task FerryDocksItselfWhenRoundstartBerthClears()
    {
        var home = await Pair.CreateTestMap();
        var maps = SEntMan.System<SharedMapSystem>();
        var ferry = SEntMan.System<OrbitraLavalandFerrySystem>();
        OrbitraLavalandRouteComponent route = default!;
        try
        {
            await Server.WaitAssertion(() =>
            {
                maps.SetTile(home.Grid, new Vector2i(0, 1), home.Tile.Tile);
                var dock = SEntMan.SpawnAttachedTo("OrbitraLavalandStationDock", new EntityCoordinates(home.Grid, .5f, .5f));
                SEntMan.SpawnAttachedTo("OrbitraLavalandStationTerminal", new EntityCoordinates(home.Grid, .5f, 1.5f));
                var obstruction = maps.CreateGridEntity(home.MapId);
                maps.SetTile(obstruction, Vector2i.Zero, home.Tile.Tile);
                var xform = SEntMan.System<SharedTransformSystem>();
                var (position, rotation) = xform.GetWorldPositionRotation(dock);
                xform.SetWorldPosition(obstruction, position + rotation.ToWorldVec() - new Vector2(.5f));

                Assert.That(ferry.TryInitializeStation(home.Grid), Is.True);
                Assert.That(ferry.TryGetRoute(out var created), Is.True);
                route = created!;
                Assert.That(route.Status, Is.EqualTo(LavalandFerryStatus.Blocked));
                Assert.That(route.StagingMap, Is.Not.Null);
                Assert.That(ferry.GetState(route.StationTerminal).CanRequest, Is.False);
                SEntMan.DeleteEntity(obstruction);
            });
            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                Assert.That(route.Status, Is.EqualTo(LavalandFerryStatus.Docked));
                Assert.That(SEntMan.GetComponent<DockingComponent>(route.StationDock).DockedWith, Is.EqualTo(route.FerryDock));
                Assert.That(SEntMan.GetComponent<TransformComponent>(route.Ferry!.Value).MapUid, Is.EqualTo(home.MapUid));
                Assert.That(route.StagingMap, Is.Null);
                Assert.That(route.Planet, Is.Null);
            });
        }
        finally
        {
            await Server.WaitPost(() => SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent()));
        }
    }

    [Test]
    public async Task ReturnTripKeepsCargoAndRetriesOccupiedLanding()
    {
        var home = await Pair.CreateTestMap();
        var ferry = SEntMan.System<OrbitraLavalandFerrySystem>();
        var maps = SEntMan.System<SharedMapSystem>();
        var xform = SEntMan.System<SharedTransformSystem>();
        var docks = SEntMan.System<DockingSystem>();
        var shuttles = SEntMan.System<ShuttleSystem>();
        var clock = Server.ResolveDependency<IGameTiming>();
        OrbitraLavalandRouteComponent route = default!;
        EntityUid cargo = default;
        EntityUid mech = default;
        EntityUid stretcher = default;
        EntityUid person = default;
        EntityUid ship = default;
        EntityUid planet = default;
        EntityUid blocker = default;
        EntityUid blockerPort = default;

        await Server.WaitAssertion(() =>
        {
            maps.SetTile(home.Grid, new Vector2i(0, 1), new Tile(1));
            var dock = SEntMan.SpawnAttachedTo("OrbitraLavalandStationDock", new EntityCoordinates(home.Grid, new Vector2(.5f, .5f)));
            var console = SEntMan.SpawnAttachedTo("OrbitraLavalandStationTerminal", new EntityCoordinates(home.Grid, new Vector2(.5f, 1.5f)));
            Assert.That(ferry.TryInitializeStation(home.Grid), Is.True);
            Assert.That(ferry.TryGetRoute(out var created), Is.True);
            route = created!;
            ship = route.Ferry!.Value;
            Assert.That(SEntMan.GetComponent<DockingComponent>(dock).DockedWith, Is.EqualTo(route.FerryDock));
            Assert.That(route.Planet, Is.Null);
            var inside = new EntityCoordinates(ship, SEntMan.GetComponent<TransformComponent>(route.FerryDock!.Value).LocalPosition - Vector2.UnitX);
            cargo = SEntMan.SpawnAttachedTo("CrateGenericSteel", inside);
            mech = SEntMan.SpawnAttachedTo("MechRipley", new EntityCoordinates(ship, -3.5f, 2.5f));
            stretcher = SEntMan.SpawnAttachedTo("RollerBed", new EntityCoordinates(ship, -4.5f, 1.5f));
            person = SEntMan.SpawnAttachedTo("MobHuman", inside);

            var duplicate = SEntMan.SpawnAttachedTo("OrbitraLavalandStationTerminal",
                new EntityCoordinates(home.Grid, new Vector2(.5f, .5f)));
            Assert.That(ferry.GetState(duplicate).CanRequest, Is.False);
            ferry.Request(duplicate, person);
            Assert.That(route.Planet, Is.Null);
            SEntMan.DeleteEntity(duplicate);

            ferry.Request(console, person);
            var departure = route.Deadline;
            planet = route.Planet!.Value;
            // Другой борт находится на орбите задолго до своего манёвра; клиент уже знает о гриде.
            var other = maps.CreateGridEntity(SEntMan.GetComponent<TransformComponent>(planet).MapID);
            blocker = other.Owner;
            maps.SetTile(other, Vector2i.Zero, new Tile(1));
            xform.SetWorldPosition(other, new Vector2(400, 400));
            blockerPort = SEntMan.SpawnAttachedTo("AirlockShuttle", new EntityCoordinates(other, .5f, .5f));
            Assert.That(route.Status, Is.EqualTo(LavalandFerryStatus.Boarding));
            Assert.That((departure!.Value - clock.CurTime).TotalSeconds, Is.EqualTo(10).Within(.1));
            ferry.Request(route.OutpostTerminal!.Value, person);
            Assert.That(route.Deadline, Is.EqualTo(departure));
            Assert.That(route.Planet, Is.EqualTo(planet));
            Assert.That(SEntMan.GetComponent<DockingComponent>(dock).Docked, Is.True);
            xform.Unanchor(route.OutpostDock!.Value);
            route.Deadline = clock.CurTime;
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(route.Status, Is.EqualTo(LavalandFerryStatus.Blocked));
            Assert.That(SEntMan.GetComponent<DockingComponent>(route.StationDock).DockedWith, Is.EqualTo(route.FerryDock));
            Assert.That(ferry.GetState(route.StationTerminal).CanRequest, Is.True);
            Assert.That(ferry.GetState(route.StationTerminal).BlockReason, Is.EqualTo(LavalandBlockReason.MissingDock));
            var target = route.OutpostDock!.Value;
            Assert.That(xform.AnchorEntity(target), Is.True);

            // Порт свободен, но плитка другой платформы попала в габарит будущей посадки.
            var obstruction = maps.CreateGridEntity(SEntMan.GetComponent<TransformComponent>(target).MapID);
            maps.SetTile(obstruction, Vector2i.Zero, new Tile(1));
            var (portPosition, rotation) = xform.GetWorldPositionRotation(target);
            xform.SetWorldPosition(obstruction, portPosition + rotation.ToWorldVec() - new Vector2(.5f));
            Assert.That(SEntMan.GetComponent<DockingComponent>(target).Docked, Is.False);
            ferry.Request(route.StationTerminal, person);
            Assert.That(ferry.GetState(route.StationTerminal).BlockReason, Is.EqualTo(LavalandBlockReason.Obstructed));
            SEntMan.DeleteEntity(obstruction);

            var building = SEntMan.SpawnAttachedTo("WallSolid", new EntityCoordinates(planet, portPosition + rotation.ToWorldVec()));
            Assert.That(SEntMan.GetComponent<TransformComponent>(building).GridUid, Is.EqualTo(planet));
            ferry.Request(route.StationTerminal, person);
            Assert.That(ferry.GetState(route.StationTerminal).BlockReason, Is.EqualTo(LavalandBlockReason.Obstructed));
            Assert.That(SEntMan.EntityExists(building), Is.True);
            SEntMan.DeleteEntity(building);

            ferry.Request(route.StationTerminal, person);
            Assert.That(route.Status, Is.EqualTo(LavalandFerryStatus.Boarding));
            route.Deadline = clock.CurTime;
        });

        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(route.Status, Is.EqualTo(LavalandFerryStatus.Transit));
            // Занимаем порт другим физическим гридом уже после начала рейса.
            var mobile = blockerPort;
            var ground = route.OutpostDock!.Value;
            Assert.That(docks.GetDockingConfig(blocker, route.Outpost!.Value), Is.Null);
            var config = docks.GetDockingConfig(blocker, route.Outpost!.Value,
                mobile, SEntMan.GetComponent<DockingComponent>(mobile), ground, SEntMan.GetComponent<DockingComponent>(ground));
            Assert.That(config, Is.Not.Null);
            shuttles.FTLDock((blocker, SEntMan.GetComponent<TransformComponent>(blocker)), config!);
            Assert.That(SEntMan.GetComponent<DockingComponent>(ground).DockedWith, Is.EqualTo(mobile));
        });
        await AdvanceFtl(ship, clock, 3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(route.Status, Is.EqualTo(LavalandFerryStatus.Blocked));
            Assert.That(route.Location, Is.EqualTo(LavalandSide.Station));
            Assert.That(ferry.GetState(route.StationTerminal).CanRequest, Is.False);
            Assert.That(ferry.GetState(route.StationTerminal).BlockReason, Is.EqualTo(LavalandBlockReason.Occupied));
            Assert.That(SEntMan.GetComponent<TransformComponent>(cargo).GridUid, Is.EqualTo(ship));
            Assert.That(SEntMan.GetComponent<TransformComponent>(person).GridUid, Is.EqualTo(ship));
            docks.UndockDocks(blocker);
            xform.SetWorldPosition(blocker, new Vector2(400, 400));
        });
        await AdvanceFtl(ship, clock, 1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(route.Location, Is.EqualTo(LavalandSide.Outpost));
            Assert.That(SEntMan.GetComponent<DockingComponent>(route.OutpostDock!.Value).DockedWith, Is.EqualTo(route.FerryDock));
            Assert.That(SEntMan.GetComponent<TransformComponent>(cargo).MapUid, Is.EqualTo(planet));
            Assert.That(SEntMan.GetComponent<TransformComponent>(mech).GridUid, Is.EqualTo(ship));
            Assert.That(SEntMan.GetComponent<TransformComponent>(stretcher).GridUid, Is.EqualTo(ship));
            ferry.Request(route.StationTerminal, person);
            route.Deadline = clock.CurTime;
        });
        await Pair.RunTicksSync(3);
        await AdvanceFtl(ship, clock, 3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(route.Location, Is.EqualTo(LavalandSide.Station));
            Assert.That(route.Planet, Is.EqualTo(planet));
            Assert.That(SEntMan.GetComponent<TransformComponent>(cargo).GridUid, Is.EqualTo(ship));
            Assert.That(SEntMan.GetComponent<TransformComponent>(person).GridUid, Is.EqualTo(ship));
            Assert.That(SEntMan.GetComponent<DockingComponent>(route.StationDock).DockedWith, Is.EqualTo(route.FerryDock));
            ferry.Request(route.StationTerminal, person);
            route.Deadline = clock.CurTime;
        });
        await Pair.RunTicksSync(3);
        await AdvanceFtl(ship, clock, 3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(route.Location, Is.EqualTo(LavalandSide.Outpost));
            ferry.Request(route.OutpostTerminal!.Value, person);
            route.Deadline = clock.CurTime;
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(route.Status, Is.EqualTo(LavalandFerryStatus.Transit));
            var prospector = SEntMan.SpawnAttachedTo("MobHuman", new EntityCoordinates(planet, 31.5f, 12.5f));
            SEntMan.DeleteEntity(home.Grid);
            Assert.That(SEntMan.EntityExists(planet), Is.True);
            Assert.That(SEntMan.EntityExists(prospector), Is.True);
            Assert.That(ferry.TryGetRoute(out var surviving), Is.True);
            Assert.That(surviving!.Planet, Is.EqualTo(planet));
            Assert.That(ferry.TryInitializeStation(route.Outpost!.Value), Is.False);
            Assert.That(route.Destination, Is.EqualTo(LavalandSide.Outpost));
        });
        await AdvanceFtl(ship, clock, 3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<DockingComponent>(route.OutpostDock!.Value).DockedWith, Is.EqualTo(route.FerryDock));
            Assert.That(SEntMan.GetComponent<TransformComponent>(person).GridUid, Is.EqualTo(ship));
            Assert.That(ferry.GetState(route.OutpostTerminal!.Value).CanRequest, Is.False);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(planet), Is.True);
            Assert.That(SEntMan.Deleted(ship), Is.True);
            Assert.That(ferry.TryGetRoute(out _), Is.False);
        });
    }

    [Test]
    public async Task PilotDrivesRecoveredRipleyAboardAndReturnsWithItsDrill()
    {
        var home = await Pair.CreateTestMap();
        var ferry = SEntMan.System<OrbitraLavalandFerrySystem>();
        var vehicle = SEntMan.System<VehicleSystem>();
        var xform = SEntMan.System<SharedTransformSystem>();
        var session = ServerSession!;
        var previous = session.AttachedEntity;
        OrbitraLavalandRouteComponent route = default!;
        EntityUid pilot = default, mech = default, drill = default, cell = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                SEntMan.System<SharedMapSystem>().SetTile(home.Grid, new Vector2i(0, 1), home.Tile.Tile);
                SEntMan.SpawnAttachedTo("OrbitraLavalandStationDock", new EntityCoordinates(home.Grid, 0.5f, 0.5f));
                var terminal = SEntMan.SpawnAttachedTo("OrbitraLavalandStationTerminal", new EntityCoordinates(home.Grid, 0.5f, 1.5f));
                pilot = SEntMan.SpawnAttachedTo("MobHuman", new EntityCoordinates(home.Grid, 0.5f, 1.5f));
                Assert.That(ferry.TryInitializeStation(home.Grid), Is.True);
                Assert.That(ferry.TryGetRoute(out var created), Is.True);
                route = created!;
                route.Seed = 23;
                ferry.Request(terminal, pilot);
            });
            await CompleteTrip();

            await Server.WaitAssertion(() =>
            {
                Assert.That(route.Location, Is.EqualTo(LavalandSide.Outpost));
                Assert.That(SEntMan.GetComponent<DockingComponent>(route.OutpostDock!.Value).DockedWith, Is.EqualTo(route.FerryDock));
                var finds = new HashSet<Entity<OrbitraSalvageMechComponent>>();
                var map = SEntMan.GetComponent<TransformComponent>(route.Planet!.Value).MapID;
                SEntMan.System<EntityLookupSystem>().GetEntitiesOnMap(map, finds);
                Assert.That(finds, Has.Count.EqualTo(1));
                mech = finds.Single().Owner;
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(mech).EntityPrototype!.ID, Is.EqualTo("OrbitraMechRipleyAbandoned"));
                var equipment = SEntMan.GetComponent<MechComponent>(mech);
                drill = equipment.EquipmentContainer.ContainedEntities.Single();
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(drill).EntityPrototype!.ID, Is.EqualTo("OrbitraMechMiningDrill"));

                var wreck = SEntMan.GetComponent<TransformComponent>(mech).Coordinates;
                var work = wreck.Offset(-Vector2.UnitY);
                xform.SetCoordinates(pilot, work);
                cell = SEntMan.SpawnAttachedTo("PowerCellSmall", work);
                var cable = SEntMan.SpawnAttachedTo("CableApcStack1", work);
                var hands = SEntMan.System<SharedHandsSystem>();
                var interact = SEntMan.System<SharedInteractionSystem>();
                Assert.That(hands.TryPickup(pilot, cell), Is.True);
                interact.InteractUsing(pilot, cell, mech, wreck);
                Assert.That(equipment.BatterySlot.ContainedEntity, Is.EqualTo(cell));
                Assert.That(hands.TryPickup(pilot, cable), Is.True);
                Assert.That(interact.InteractUsing(pilot, cable, mech, wreck), Is.True);
            });
            await Pair.RunSeconds(6);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<OrbitraSalvageMechComponent>(mech).DriveRepaired, Is.True);
                var port = SEntMan.GetComponent<TransformComponent>(route.OutpostDock!.Value);
                // Пропускаем путь от руины. Отсюда оба шлюза проходятся обычным вводом пилота.
                var approach = port.Coordinates.Offset(-port.LocalRotation.ToWorldVec());
                xform.SetCoordinates(mech, approach);
                xform.SetCoordinates(pilot, approach.Offset(-Vector2.UnitY));
                Server.PlayerMan.SetAttachedEntity(session, pilot);
                ferry.Request(route.OutpostTerminal!.Value, pilot);
                Assert.That(route.Status, Is.EqualTo(LavalandFerryStatus.Boarding));
                Assert.That(vehicle.TryEnter(mech, pilot), Is.True);
                Assert.That(SEntMan.GetComponent<TransformComponent>(mech).GridUid, Is.EqualTo(route.Outpost));
            });
            await Pair.RunUntilSynced();
            await MoveNorth(BoundKeyState.Down);
            await Pair.RunSeconds(2);
            await MoveNorth(BoundKeyState.Up);
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
            {
                var boarded = SEntMan.GetComponent<TransformComponent>(mech);
                var door = SEntMan.GetComponent<TransformComponent>(route.FerryDock!.Value);
                Assert.That(boarded.GridUid, Is.EqualTo(route.Ferry));
                Assert.That(Vector2.Dot(door.LocalPosition - boarded.LocalPosition, door.LocalRotation.ToWorldVec()), Is.GreaterThan(0.5f));
            });

            await CompleteTrip();
            await Server.WaitAssertion(() =>
            {
                Assert.That(route.Location, Is.EqualTo(LavalandSide.Station));
                Assert.That(SEntMan.GetComponent<DockingComponent>(route.StationDock).DockedWith, Is.EqualTo(route.FerryDock));
                var returned = SEntMan.GetComponent<TransformComponent>(mech);
                Assert.That(returned.MapUid, Is.EqualTo(home.MapUid));
                Assert.That(returned.GridUid, Is.EqualTo(route.Ferry));
                Assert.That(vehicle.GetOperatorOrNull(mech), Is.EqualTo(pilot));
                var equipment = SEntMan.GetComponent<MechComponent>(mech);
                Assert.That(equipment.EquipmentContainer.ContainedEntities, Does.Contain(drill));
                Assert.That(equipment.BatterySlot.ContainedEntity, Is.EqualTo(cell));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, previous);
                SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            });
            await Pair.RunTicksSync(2);
        }

        async Task CompleteTrip()
        {
            await Server.WaitPost(() => route.Deadline = SGameTiming.CurTime);
            await Pair.RunSeconds(1);
            await Server.WaitAssertion(() => Assert.That(route.Status, Is.EqualTo(LavalandFerryStatus.Transit)));
            await AdvanceFtl(route.Ferry!.Value, SGameTiming, 3);
        }

        Task MoveNorth(BoundKeyState state) => Client.WaitPost(() =>
        {
            var key = EngineKeyFunctions.MoveUp;
            var id = Client.ResolveDependency<IInputManager>().NetworkBindMap.KeyFunctionID(key);
            var input = new ClientFullInputCmdMessage(Client.Timing.CurTick, Client.Timing.TickFraction, id) { State = state };
            Client.System<Robust.Client.GameObjects.InputSystem>().HandleInputCommand(Client.Session, key, input);
        });
    }

    private async Task AdvanceFtl(EntityUid ship, IGameTiming clock, int phases)
    {
        for (var i = 0; i < phases; i++)
        {
            await Server.WaitPost(() =>
            {
                if (SEntMan.TryGetComponent<FTLComponent>(ship, out var ftl))
                    ftl.StateTime = StartEndTime.FromStartDuration(clock.CurTime, TimeSpan.Zero);
            });
            await Pair.RunTicksSync(2);
        }
    }
}
