// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.IntegrationTests.Pair;
using System.Linq;
using Content.Client._Orbitra.Xenobiology;
using Content.Server._Orbitra.Xenobiology;
using Content.Server.Lathe;
using Content.Server.Power.EntitySystems;
using Content.Shared._Orbitra.Xenobiology;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Lathe;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Client.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra.Xenobiology;

[TestFixture, NonParallelizable]
public sealed class OrbitraXenobiologyTest
{
    [Test]
    public async Task LaboratoryContentIsAvailable()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
        await pair.Server.WaitAssertion(() =>
        {
            foreach (var id in new[] { "OrbitraXenoSlimeGrey", "OrbitraXenoSlimeGreyAdult", "OrbitraXenoProcessor",
                         "OrbitraXenoCarrier", "OrbitraXenoConsole", "OrbitraXenoCultureGrey", "OrbitraXenoExtractGrey" })
            {
                Assert.That(prototypes.HasIndex<EntityPrototype>(id), Is.True, $"Missing laboratory entity {id}.");
            }
            var lathes = pair.GetPrototypesWithComponent<LatheComponent>().ToList();
            var lathe = pair.Server.System<LatheSystem>();
            var carriers = lathe.GetAllPossibleRecipes(lathes.Single(entry => entry.Item1.ID == "Protolathe").Item2)
                .Select(recipe => recipe.ToString()).ToArray();
            var boards = lathe.GetAllPossibleRecipes(lathes.Single(entry => entry.Item1.ID == "CircuitImprinter").Item2)
                .Select(recipe => recipe.ToString()).ToArray();
            Assert.That(carriers, Does.Contain("OrbitraXenoCarrier").And.Contain("OrbitraXenoCarrierAdvanced"));
            Assert.That(boards, Does.Contain("OrbitraXenoProcessorBoard").And.Contain("OrbitraXenoConsoleBoard")
                .And.Contain("OrbitraXenoThermostatBoard"));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CarrierTransfersCorpseAndPowerLossDoesNotSpendItsExtracts()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var devices = server.System<OrbitraXenobiologyDeviceSystem>();
        var containers = server.System<SharedContainerSystem>();
        EntityUid user = default, carrier = default, corpse = default, processor = default;
        var captured = false;
        var transferred = false;
        var started = false;
        var completed = false;
        try
        {
            await server.WaitPost(() =>
            {
                var mapSystem = server.System<SharedMapSystem>();
                for (var x = 0; x < 5; x++)
                for (var y = 0; y < 4; y++)
                    mapSystem.SetTile(map.Grid, new EntityCoordinates(map.Grid, x, y), map.Tile.Tile);
                var coordinates = new EntityCoordinates(map.Grid, 1.5f, 1.5f);
                user = server.EntMan.SpawnEntity("MobHuman", coordinates);
                server.System<SharedGodmodeSystem>().EnableGodmode(user);
                carrier = server.EntMan.SpawnEntity("OrbitraXenoCarrier", coordinates);
                corpse = server.EntMan.SpawnEntity("OrbitraXenoSlimeGreyAdult", new EntityCoordinates(map.Grid, 2.25f, 1.5f));
                processor = server.EntMan.SpawnEntity("OrbitraXenoProcessor", new EntityCoordinates(map.Grid, 1.5f, 2.5f));
                server.System<MobStateSystem>().ChangeMobState(corpse, MobState.Dead);
                server.System<SharedHandsSystem>().TryPickupAnyHand(user, carrier);
                captured = devices.TryCapture((carrier, server.EntMan.GetComponent<OrbitraXenobiologyCarrierComponent>(carrier)), user, corpse);
            });
            await pair.RunSeconds(2.4f);
            await server.WaitAssertion(() =>
            {
                Assert.That(captured, Is.True);
                Assert.That(containers.IsEntityInContainer(corpse), Is.True);
                Assert.That(server.EntMan.GetComponent<OrbitraXenobiologySlimeComponent>(corpse).Suspended, Is.True);
            });
            await server.WaitPost(() =>
            {
                var machine = (processor, server.EntMan.GetComponent<OrbitraXenobiologyProcessorComponent>(processor));
                transferred = devices.TryTransfer((carrier, server.EntMan.GetComponent<OrbitraXenobiologyCarrierComponent>(carrier)), user, machine);
                server.System<PowerReceiverSystem>().SetNeedsPower(processor, false);
            });
            await pair.RunTicksSync(3);
            await server.WaitPost(() => started = devices.TryProcess(
                (processor, server.EntMan.GetComponent<OrbitraXenobiologyProcessorComponent>(processor)), user));
            await pair.RunSeconds(2f);
            await server.WaitAssertion(() =>
            {
                Assert.That(transferred, Is.True);
                Assert.That(started, Is.True);
                Assert.That(server.EntMan.GetComponent<OrbitraXenobiologySlimeComponent>(corpse).Consumed, Is.False);
            });
            await server.WaitPost(() => server.System<PowerReceiverSystem>().SetNeedsPower(processor, true));
            await pair.RunSeconds(5f);
            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.EntityExists(corpse), Is.True);
                Assert.That(server.EntMan.GetComponent<OrbitraXenobiologySlimeComponent>(corpse).Consumed, Is.False);
                Assert.That(server.EntMan.HasComponent<OrbitraXenobiologyProcessingComponent>(processor), Is.False);
            });
            await server.WaitPost(() =>
            {
                server.System<PowerReceiverSystem>().SetNeedsPower(processor, false);
            });
            await pair.RunTicksSync(3);
            await server.WaitPost(() => started = devices.TryProcess(
                (processor, server.EntMan.GetComponent<OrbitraXenobiologyProcessorComponent>(processor)), user));
            await pair.RunSeconds(6.2f);
            await server.WaitAssertion(() =>
            {
                Assert.That(started, Is.True);
                Assert.That(server.EntMan.EntityExists(corpse), Is.False);
                Assert.That(server.EntMan.EntityQueryEnumerator<OrbitraXenobiologyExtractComponent>()
                    .MoveNext(out _, out _), Is.True);
            });
            completed = true;
        }
        finally
        {
            await server.WaitPost(() => server.EntMan.DeleteEntity(map.MapUid));
            if (completed)
                await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task ConsoleOpensNativeClientWindowWithMeasurementsAndCatalog()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var previous = pair.Player.AttachedEntity;
        EntityUid console = default;
        var opened = false;
        var completed = false;
        try
        {
            await server.WaitPost(() =>
            {
                var mapSystem = server.System<SharedMapSystem>();
                for (var x = 0; x < 5; x++)
                for (var y = 0; y < 4; y++)
                    mapSystem.SetTile(map.Grid, new EntityCoordinates(map.Grid, x, y), map.Tile.Tile);
                console = server.EntMan.SpawnEntity("OrbitraXenoConsole", new EntityCoordinates(map.Grid, 1.5f, 2.5f));
                server.System<PowerReceiverSystem>().SetNeedsPower(console, false);
                var user = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 1.5f, 1.5f));
                server.EntMan.SpawnEntity("OrbitraXenoSlimeGrey", new EntityCoordinates(map.Grid, 2.25f, 1.5f));
                server.EntMan.SpawnEntity("OrbitraXenoSlimeGrey", new EntityCoordinates(map.Grid, 3.25f, 1.5f));
                server.PlayerMan.SetAttachedEntity(pair.Player, user);
                opened = server.System<UserInterfaceSystem>().TryOpenUi(console, OrbitraXenobiologyUiKey.Key, user);
            });
            await pair.RunTicksSync(20);
            await pair.RunSeconds(1.2f);
            await server.WaitAssertion(() =>
            {
                Assert.That(opened, Is.True);
                Assert.That(server.System<UserInterfaceSystem>()
                    .TryGetUiState<OrbitraXenobiologyBoundUserInterfaceState>(console, OrbitraXenobiologyUiKey.Key, out var snapshot), Is.True);
                var state = snapshot!;
                Assert.That(state.Specimens.Count, Is.EqualTo(2));
                Assert.That(state.Specimens.Select(specimen => specimen.Entity).Distinct().Count(), Is.EqualTo(2));
                Assert.That(state.Specimens.Select(specimen => specimen.Identifier), Is.All.Not.Empty);
                Assert.That(state.Specimens.Select(specimen => specimen.Identifier).Distinct().Count(), Is.EqualTo(2),
                    "Two individuals of the same breed must have distinct visible identifiers.");
            });
            await pair.Client.WaitAssertion(() =>
            {
                var root = pair.Client.ResolveDependency<IUserInterfaceManager>().WindowRoot;
                var window = root.Children.OfType<OrbitraXenobiologyWindow>().Single();
                Assert.That(window.SpecimenList.Children.Count(child => child.Visible), Is.EqualTo(2));
                Assert.That(window.SelectedBreed, Is.Not.Null.And.Not.Empty);
                Assert.That(window.BreedDetails.ChildCount, Is.GreaterThan(0));
                Assert.That(root.Children.OfType<OrbitraXenobiologyProcessorWindow>(), Is.Empty);
            });
            completed = true;
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.System<UserInterfaceSystem>().CloseUi(console, OrbitraXenobiologyUiKey.Key);
                server.PlayerMan.SetAttachedEntity(pair.Player, previous);
                server.EntMan.DeleteEntity(map.MapUid);
            });
            await pair.RunTicksSync(5);
            if (completed)
                await pair.CleanReturnAsync();
        }
    }
}
