using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra.Atmos;

[TestFixture]
public sealed class DevBreachTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = false,
        DummyTicker = false,
        Dirty = true,
        Map = "Dev",
    };

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task HullFailureRespectsAirtightDoor(bool doorOpen, bool slowLeak)
    {
        var maps = Server.System<SharedMapSystem>();
        var transform = Server.System<SharedTransformSystem>();
        var atmosphere = Server.System<AtmosphereSystem>();
        EntityUid grid = default;
        EntityUid bulkhead = default;
        EntityUid hull = default;
        EntityUid exposedPaper = default;
        EntityUid interiorPaper = default;
        EntityUid seat = default;
        EntityUid occupant = default;
        EntityCoordinates interiorStart = default;
        EntityCoordinates seatStart = default;

        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.SpaceWind, true);
            var spawns = SEntMan.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (spawns.MoveNext(out _, out var metadata, out var xform))
            {
                if (metadata.EntityPrototype?.ID != "SpawnPointCaptain")
                    continue;

                var testGrid = maps.CreateGridEntity(xform.MapID);
                grid = testGrid.Owner;
                transform.SetWorldPosition(grid, transform.GetWorldPosition(xform) + new Vector2(150, 0));
                var tileId = Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId;
                var tiles = new List<(Vector2i, Tile)>();
                for (var x = -2; x <= 6; x++)
                for (var y = -1; y <= 1; y++)
                    tiles.Add((new Vector2i(x, y), new Tile(tileId)));
                maps.SetTiles(testGrid, tiles);

                EntityCoordinates At(int x, int y = 0) => maps.ToCenterCoordinates(grid, new Vector2i(x, y));
                for (var x = -2; x <= 6; x++)
                {
                    SEntMan.SpawnAttachedTo("WallSolid", At(x, -1));
                    SEntMan.SpawnAttachedTo("WallSolid", At(x, 1));
                }

                SEntMan.SpawnAttachedTo("WallSolid", At(-2));
                hull = SEntMan.SpawnAttachedTo("WallSolid", At(6));
                bulkhead = SEntMan.SpawnAttachedTo("AirlockGlass", At(0));
                if (doorOpen)
                {
                    var airlock = SEntMan.GetComponent<AirlockComponent>(bulkhead);
                    Server.System<SharedAirlockSystem>().SetAutoCloseDelayModifier(airlock, 100f);
                    Server.System<SharedDoorSystem>().StartOpening(bulkhead);
                }

                interiorStart = At(-1);
                interiorPaper = SEntMan.SpawnAttachedTo("Paper", interiorStart);
                exposedPaper = SEntMan.SpawnAttachedTo("Paper", At(5));
                if (!doorOpen && !slowLeak)
                {
                    seatStart = At(3);
                    seat = SEntMan.SpawnAttachedTo("Chair", seatStart);
                    occupant = SEntMan.SpawnAttachedTo("MobHuman", seatStart);
                    Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(occupant, occupant, seat), Is.True);
                }

                break;
            }

            Assert.That(grid.IsValid(), Is.True, "Dev captain spawn not found.");
        });

        await Server.WaitRunTicks(70);
        await Server.WaitPost(() =>
        {
            for (var x = -1; x <= 5; x++)
            {
                if (x == 0 && !doorOpen)
                    continue;

                var air = atmosphere.GetTileMixture(grid, null, new Vector2i(x, 0), excite: true);
                Assert.That(air, Is.Not.Null);
                air!.Clear();
                air.Temperature = Atmospherics.T20C;
                air.SetMoles(Gas.Oxygen, Atmospherics.MolesCellStandard * 0.21f);
                air.SetMoles(Gas.Nitrogen, Atmospherics.MolesCellStandard * 0.79f);
            }
        });

        await Server.WaitRunTicks(12);
        await Server.WaitPost(() =>
        {
            Assert.That(atmosphere.IsTileAirBlockedCached(grid, Vector2i.Zero), Is.EqualTo(!doorOpen));
            if (slowLeak)
            {
                Server.System<DamageableSystem>().TryChangeDamage(hull,
                    new DamageSpecifier { DamageDict = { ["Blunt"] = 180 } }, ignoreResistances: true);
                return;
            }

            SEntMan.DeleteEntity(hull);
            maps.SetTile(grid, SEntMan.GetComponent<MapGridComponent>(grid), new Vector2i(6, 0), Tile.Empty);
        });

        await Server.WaitRunTicks(25);
        if (seat.IsValid())
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<TransformComponent>(seat).Coordinates, Is.EqualTo(seatStart));
                Assert.That(SEntMan.GetComponent<BuckleComponent>(occupant).BuckledTo, Is.EqualTo(seat));
            });
        }

        await Server.WaitRunTicks(215);
        await Server.WaitAssertion(() =>
        {
            var interiorPressure = atmosphere.GetTileMixture(grid, null, new Vector2i(-1, 0))!.Pressure;
            var exteriorPressure = atmosphere.GetTileMixture(grid, null, new Vector2i(5, 0))!.Pressure;
            Assert.Multiple(() =>
            {
                if (slowLeak)
                {
                    Assert.That(SEntMan.EntityExists(hull), Is.True);
                    Assert.That(SEntMan.GetComponent<TransformComponent>(exposedPaper).GridUid, Is.EqualTo(grid));
                    Assert.That(exteriorPressure, Is.InRange(65f, 99f), "The damaged wall must leak without an explosive blowout.");
                }
                else
                {
                    Assert.That(SEntMan.GetComponent<GridAtmosphereComponent>(grid).Tiles.Values
                        .Any(tile => tile.OrbitraNextBreachVisual > TimeSpan.Zero), Is.True,
                        "An open hull breach must emit its visual cue.");
                    Assert.That(SEntMan.GetComponent<TransformComponent>(exposedPaper).GridUid, Is.Null, "Paper must leave the grid into space.");
                    Assert.That(exteriorPressure, Is.LessThan(15f));
                }

                if (doorOpen)
                {
                    Assert.That(interiorPressure, Is.LessThan(85f));
                    Assert.That(SEntMan.GetComponent<TransformComponent>(interiorPaper).Coordinates, Is.Not.EqualTo(interiorStart));
                }
                else
                {
                    Assert.That(interiorPressure, Is.GreaterThan(90f));
                    Assert.That(SEntMan.GetComponent<TransformComponent>(interiorPaper).Coordinates, Is.EqualTo(interiorStart));
                }
            });
        });

        if (!slowLeak)
            return;

        await Server.WaitPost(() =>
        {
            var coordinates = maps.ToCenterCoordinates(grid, new Vector2i(5, 0));
            var engineer = SEntMan.SpawnAttachedTo("MobHuman", coordinates);
            var welder = SEntMan.SpawnAttachedTo("Welder", coordinates);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(engineer, welder), Is.True);
            Assert.That(Server.System<ItemToggleSystem>().TryActivate(welder, engineer), Is.True);
            var interaction = new InteractUsingEvent(engineer, welder, hull, SEntMan.GetComponent<TransformComponent>(hull).Coordinates);
            SEntMan.EventBus.RaiseLocalEvent(hull, interaction, true);
            Assert.That(interaction.Handled, Is.True, "Welder must start the hull patch.");
        });

        await Server.WaitRunTicks(150);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<OrbitraHullLeakComponent>(hull).PatchedUntil, Is.GreaterThan(SGameTiming.CurTime));
            Assert.That(SEntMan.GetComponent<OrbitraActiveHullLeakComponent>(hull).Flowing, Is.False);
        });
    }
}
