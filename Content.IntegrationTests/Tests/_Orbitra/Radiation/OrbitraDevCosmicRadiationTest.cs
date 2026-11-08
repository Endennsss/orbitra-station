// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Radiation;
using Content.Server.GameTicking;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests._Orbitra.Radiation;

[TestFixture]
public sealed class OrbitraDevCosmicRadiationTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = false,
        DummyTicker = false,
        Dirty = true,
        Map = "Dev",
    };

    [Test]
    public async Task ActualDevMapExposesSpaceAndLatticeButProtectsSpawnRoom()
    {
        EntityUid room = default, space = default, lattice = default;
        float roomRate = -1, spaceRate = -1, latticeRate = -1;
        var foundRoom = false;
        var foundLattice = false;
        await Server.WaitPost(() =>
        {
            var cosmic = Server.System<OrbitraCosmicRadiationSystem>();
            var maps = Server.System<SharedMapSystem>();
            var map = maps.GetMapOrInvalid(Server.System<GameTicker>().DefaultMap);
            var spawns = SEntMan.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (spawns.MoveNext(out _, out var metadata, out var transform))
            {
                if (metadata.EntityPrototype?.ID != "SpawnPointCaptain")
                    continue;
                room = SEntMan.SpawnEntity("MobHuman", transform.Coordinates);
                foundRoom = true;
                roomRate = cosmic.GetExposure(room);
                break;
            }
            space = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map, new Vector2(10000, 10000)));
            spaceRate = cosmic.GetExposure(space);
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var grids = SEntMan.AllEntityQueryEnumerator<MapGridComponent, TransformComponent>();
            while (!foundLattice && grids.MoveNext(out var gridUid, out var grid, out var transform))
            {
                if (transform.MapUid != map)
                    continue;
                foreach (var tile in maps.GetAllTiles(gridUid, grid))
                {
                    if (tiles[tile.Tile.TypeId].ID != "Lattice")
                        continue;
                    lattice = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(gridUid,
                        new Vector2(tile.GridIndices.X + 0.5f, tile.GridIndices.Y + 0.5f)));
                    foundLattice = true;
                    latticeRate = cosmic.GetExposure(lattice);
                    break;
                }
            }
        });
        await Server.WaitAssertion(() => Assert.Multiple(() =>
        {
            Assert.That(foundRoom, Is.True, "Actual Dev captain spawn must exist.");
            Assert.That(foundLattice, Is.True, "Actual Dev exterior lattice must exist.");
            Assert.That(roomRate, Is.Zero, "The captain spawn room must be sheltered.");
            Assert.That(spaceRate, Is.EqualTo(0.3f).Within(0.001f), "Empty space must have ambient radiation.");
            Assert.That(latticeRate, Is.EqualTo(0.3f).Within(0.001f), "Exterior lattice must not shield cosmic radiation.");
        }));
        await Pair.RunTicksSync(90);
        await Server.WaitAssertion(() =>
        {
            var damage = Server.System<DamageableSystem>();
            Assert.That(damage.GetAllDamage(space).DamageDict["Radiation"].Float(), Is.GreaterThan(0));
            Assert.That(damage.GetAllDamage(lattice).DamageDict["Radiation"].Float(), Is.GreaterThan(0));
        });
    }
}
