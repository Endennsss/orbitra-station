// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System;
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server._Orbitra.Radiation;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Radiation.Systems;
using Content.Shared._Orbitra.Radiation;
using Content.Shared.Atmos.Components;
using Content.Server.Atmos.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Radiation.Components;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Tests._Orbitra.Radiation;

[TestFixture, NonParallelizable]
public sealed class OrbitraCosmicRadiationTest
{
    [Test]
    public async Task OpenSpaceCausesPersistentTreatableRadiationDamage()
    {
        await WithHuman(async (pair, map, human) =>
        {
            var server = pair.Server;
            float intensity = 0;
            await server.WaitPost(() => intensity = server.System<OrbitraCosmicRadiationSystem>().ApplyExposure(human, 10));
            await server.WaitAssertion(() =>
            {
                Assert.That(intensity, Is.EqualTo(0.3f).Within(0.001f));
                Assert.That(Radiation(pair, human), Is.EqualTo(3).Within(0.01));
            });
            await server.WaitPost(() =>
            {
                server.System<SharedTransformSystem>().SetCoordinates(human, map.GridCoords);
                server.EntMan.EnsureComponent<ImplicitRoofComponent>(map.GridCoords.EntityId);
                server.System<OrbitraCosmicRadiationSystem>().ApplyExposure(human, 600);
            });
            await server.WaitAssertion(() => Assert.That(Radiation(pair, human), Is.EqualTo(3).Within(0.01)));
            await server.WaitPost(() => server.System<DamageableSystem>().ChangeDamage(human,
                new DamageSpecifier { DamageDict = { ["Radiation"] = FixedPoint2.New(-3) } }));
            await server.WaitAssertion(() => Assert.That(Radiation(pair, human), Is.Zero));
        });
    }

    [TestCase("ClothingOuterHardsuitEVA", 0.2f, 250f)]
    [TestCase("ClothingOuterHardsuitSpatio", 0.12f, 417f)]
    [TestCase("ClothingOuterHardsuitEngineering", 0.08f, 625f)]
    [TestCase("ClothingOuterHardsuitEngineeringWhite", 0.07f, 715f)]
    [TestCase("ClothingOuterSuitSpaceNinja", 0.12f, 417f)]
    [TestCase("ClothingOuterHardsuitCarp", 0.12f, 417f)]
    public async Task EquippedSuitDefinesCosmicWorkBudget(string prototype, float transmission, float seconds)
    {
        await WithHuman(async (pair, map, human) =>
        {
            var server = pair.Server;
            var equipped = false;
            float actual = 0;
            await server.WaitPost(() =>
            {
                var suit = server.EntMan.SpawnEntity(prototype, map.GridCoords);
                var helmet = server.EntMan.SpawnEntity("ClothingHeadHelmetEVA", map.GridCoords);
                var inventory = server.System<InventorySystem>();
                equipped = inventory.TryEquip(human, suit, "outerClothing", force: true)
                    && inventory.TryEquip(human, helmet, "head", force: true);
                var cosmic = server.System<OrbitraCosmicRadiationSystem>();
                actual = cosmic.GetTransmission(human);
                for (var second = 0; second < (int) seconds; second++)
                    cosmic.ApplyExposure(human, 1f);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(equipped, Is.True);
                Assert.That(actual, Is.EqualTo(transmission).Within(0.0001f));
                Assert.That(Radiation(pair, human), Is.InRange(14.9f, 15.1f));
            });
        });
    }

    [Test]
    public async Task ProtectionRequiresWornSuitAndSealedHeadSlot()
    {
        await WithHuman(async (pair, map, human) =>
        {
            var values = new List<float>();
            await pair.Server.WaitPost(() =>
            {
                var entities = pair.Server.EntMan;
                var inventory = pair.Server.System<InventorySystem>();
                var cosmic = pair.Server.System<OrbitraCosmicRadiationSystem>();
                var suit = entities.SpawnEntity("ClothingOuterHardsuitEVA", map.GridCoords);
                var helmet = entities.SpawnEntity("ClothingHeadHelmetEVA", map.GridCoords);
                values.Add(cosmic.GetTransmission(human));
                inventory.TryEquip(human, suit, "outerClothing", force: true);
                values.Add(cosmic.GetTransmission(human));
                inventory.TryEquip(human, helmet, "head", force: true);
                values.Add(cosmic.GetTransmission(human));
                inventory.TryUnequip(human, "head", force: true);
                values.Add(cosmic.GetTransmission(human));
                var invalid = entities.GetComponent<OrbitraCosmicRadiationProtectionComponent>(suit);
                invalid.Transmission = float.NaN;
                inventory.TryEquip(human, helmet, "head", force: true);
                values.Add(cosmic.GetTransmission(human));
                invalid.Transmission = 0f;
                values.Add(cosmic.GetTransmission(human));
            });
            Assert.That(values, Is.EqualTo(new[] { 1f, 1f, 0.2f, 1f, 1f, 1f }));
        });
    }

    [Test]
    public async Task SheltersPlanetsPausedEntitiesGhostsAndInvalidDurationsAreSafe()
    {
        await WithHuman(async (pair, map, human) =>
        {
            var rates = new List<float>();
            await pair.Server.WaitPost(() =>
            {
                var server = pair.Server;
                var cosmic = server.System<OrbitraCosmicRadiationSystem>();
                server.System<SharedTransformSystem>().SetCoordinates(human, map.GridCoords);
                server.EntMan.EnsureComponent<ImplicitRoofComponent>(map.GridCoords.EntityId);
                rates.Add(cosmic.GetExposure(human));
                server.System<SharedTransformSystem>().SetCoordinates(human, new EntityCoordinates(map.MapUid, new Vector2(100, 100)));
                rates.Add(cosmic.GetExposure(human));
                server.System<MetaDataSystem>().SetEntityPaused(human, true);
                rates.Add(cosmic.GetExposure(human));
                server.System<MetaDataSystem>().SetEntityPaused(human, false);
                server.EntMan.EnsureComponent<MapAtmosphereComponent>(map.MapUid);
                server.System<AtmosphereSystem>().SetMapSpace(map.MapUid, false, updateTiles: false);
                rates.Add(cosmic.GetExposure(human));
                server.System<AtmosphereSystem>().SetMapSpace(map.MapUid, true, updateTiles: false);
                var ghost = server.EntMan.SpawnEntity("MobObserver", new EntityCoordinates(map.MapUid, new Vector2(100, 100)));
                rates.Add(cosmic.GetExposure(ghost));
                rates.Add(cosmic.ApplyExposure(human, float.NaN));
                rates.Add(cosmic.ApplyExposure(human, -10));
                rates.Add(cosmic.ApplyExposure(human, float.PositiveInfinity));
            });
            Assert.That(rates, Is.EqualTo(new[] { 0f, 0.3f, 0f, 0f, 0f, 0f, 0f, 0f }));
        });
    }

    [Test]
    public async Task ExistingGeigerCounterDetectsCosmicBackgroundAndOrdinaryRadiationStillWorks()
    {
        await WithHuman(async (pair, map, human) =>
        {
            var server = pair.Server;
            EntityUid geiger = default;
            await server.WaitPost(() =>
            {
                geiger = server.EntMan.SpawnEntity("GeigerCounter", new EntityCoordinates(map.MapUid, new Vector2(100, 100)));
                server.EntMan.EventBus.RaiseLocalEvent(geiger, new ActivateInWorldEvent(human, geiger, true));
            });
            await pair.RunTicksSync(90);
            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.GetComponent<GeigerComponent>(geiger).CurrentRadiation,
                    Is.EqualTo(0.3f).Within(0.001f));
                Assert.That(Radiation(pair, human), Is.GreaterThan(0));
            });
            float before = 0;
            await server.WaitPost(() =>
            {
                before = Radiation(pair, human);
                server.System<RadiationSystem>().IrradiateEntity(human, 1f, 1f);
            });
            await server.WaitAssertion(() => Assert.That(Radiation(pair, human), Is.EqualTo(before + 1f).Within(0.01f)));
        });
    }

    [Test]
    public async Task ShortTicksPreserveFractionalDose()
    {
        await WithHuman(async (pair, map, human) =>
        {
            await pair.Server.WaitPost(() =>
            {
                var cosmic = pair.Server.System<OrbitraCosmicRadiationSystem>();
                for (var tick = 0; tick < 1000; tick++)
                    cosmic.ApplyExposure(human, 0.01f);
            });
            await pair.Server.WaitAssertion(() => Assert.That(Radiation(pair, human), Is.EqualTo(3f).Within(0.02f)));
        });
    }

    [Test]
    public async Task ExplicitRoofAndNullspaceDoNotExposeEntities()
    {
        await WithHuman(async (pair, map, human) =>
        {
            var values = new List<float>();
            await pair.Server.WaitPost(() =>
            {
                var server = pair.Server;
                var cosmic = server.System<OrbitraCosmicRadiationSystem>();
                var grid = map.GridCoords.EntityId;
                server.System<SharedTransformSystem>().SetCoordinates(human, map.GridCoords);
                server.EntMan.RemoveComponent<ImplicitRoofComponent>(grid);
                server.EntMan.EnsureComponent<RoofComponent>(grid);
                var indices = server.System<SharedTransformSystem>().GetGridOrMapTilePosition(human);
                server.System<SharedRoofSystem>().SetRoof(grid, indices, true);
                values.Add(cosmic.GetExposure(human));
                server.System<SharedRoofSystem>().SetRoof(grid, indices, false);
                values.Add(cosmic.GetExposure(human));
                var nullspace = server.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                values.Add(cosmic.GetExposure(nullspace));
                server.EntMan.DeleteEntity(nullspace);
            });
            Assert.That(values, Is.EqualTo(new[] { 0f, 0.3f, 0f }));
        });
    }

    [Test]
    public async Task EveryVacuumProtectiveOuterClothingHasCosmicShielding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();
        await server.WaitAssertion(() =>
        {
            var checkedSuits = 0;
            Assert.Multiple(() =>
            {
                foreach (var prototype in prototypes.EnumeratePrototypes<EntityPrototype>())
                {
                    if (prototype.Abstract || pair.IsTestPrototype(prototype)
                        || !prototype.TryComp<ClothingComponent>(out var clothing, factory)
                        || (clothing.Slots & SlotFlags.OUTERCLOTHING) == 0
                        || !prototype.TryComp<PressureProtectionComponent>(out var pressure, factory)
                        || pressure.LowPressureMultiplier <= 1f)
                        continue;
                    checkedSuits++;
                    Assert.That(prototype.TryComp<OrbitraCosmicRadiationProtectionComponent>(out var protection, factory),
                        Is.True, $"{prototype.ID} protects from vacuum but has no cosmic shielding.");
                    if (protection != null)
                        Assert.That(protection.Transmission, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f), prototype.ID);
                }
            });
            Assert.That(checkedSuits, Is.GreaterThan(0));
        });
        await pair.CleanReturnAsync();
    }

    private static float Radiation(TestPair pair, EntityUid uid)
    {
        return pair.Server.System<DamageableSystem>().GetAllDamage(uid).DamageDict["Radiation"].Float();
    }

    private static async Task WithHuman(Func<TestPair, TestMapData, EntityUid, Task> action)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid human = default;
        try
        {
            await pair.Server.WaitPost(() => human = pair.Server.EntMan.SpawnEntity("MobHuman",
                new EntityCoordinates(map.MapUid, new Vector2(100, 100))));
            await action(pair, map, human);
        }
        finally
        {
            await pair.Server.WaitPost(() => pair.Server.EntMan.DeleteEntity(map.MapUid));
        }
        await pair.CleanReturnAsync();
    }
}
