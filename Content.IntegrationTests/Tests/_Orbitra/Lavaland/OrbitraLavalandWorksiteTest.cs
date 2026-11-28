using System;
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Lavaland;
using Content.Server.Lathe;
using Content.Server.Materials;
using Content.Server.Power.Generator;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Research.Prototypes;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra.Lavaland;

public sealed class OrbitraLavalandWorksiteTest : GameTest
{
    private static readonly ProtoId<LatheRecipePrototype> GoldIngot = "IngotGold1";
    private EntityUid _surface;
    private EntityUid _outpost;

    [SetUp]
    public async Task CreateSurface()
    {
        await Server.WaitAssertion(() => Assert.That(SEntMan.System<OrbitraLavalandSurfaceSystem>()
            .TryCreate(23, out _surface, out _outpost), Is.True));
        await Pair.RunSeconds(2);
    }

    [TearDown]
    public async Task RemoveSurface()
    {
        await Server.WaitPost(() => SEntMan.DeleteEntity(_surface));
    }

    [Test]
    public async Task GreenhouseSeedsCanBeSalvagedAndGrowAfterCableRepair()
    {
        EntityUid greenhouse = default, tray = default, worker = default, plant = default, regularPlant = default;
        var fixtures = new HashSet<Entity<PoweredLightComponent, PointLightComponent>>();
        await Server.WaitAssertion(() =>
        {
            greenhouse = Site("orbitra-lavaland-ruin-preserved-terrarium");
            tray = Find(greenhouse, "OrbitraLavalandHydroponicsTray");
            worker = SEntMan.SpawnAttachedTo("MobHuman", Coordinates(tray));
            var crate = Find(greenhouse, "CrateHydroponicsSeedsMedicinal");
            Assert.That(SEntMan.System<SharedEntityStorageSystem>().TryOpenStorage(worker, crate), Is.True);
            Assert.That(Find(greenhouse, "AloeSeeds").Valid, Is.True);
            Assert.That(SEntMan.System<SharedPowerReceiverSystem>().IsPowered(tray), Is.False);
            SEntMan.System<EntityLookupSystem>().GetChildEntities(greenhouse, fixtures);
            Assert.That(fixtures.Any(f => !f.Comp2.Enabled), Is.True);
            var seeds = Find(greenhouse, "TomatoSeeds");
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(worker, seeds), Is.True);
            Assert.That(SEntMan.System<SharedInteractionSystem>().InteractUsing(worker, seeds, tray, Coordinates(tray)), Is.True);
            Assert.That(SEntMan.System<PlantTraySystem>().TryGetPlant(tray, out var planted), Is.True);
            plant = planted!.Value;
            // Ускоряем только таймер культуры, сохраняя обычные циклы роста, питания и созревания.
            SEntMan.GetComponent<PlantHolderComponent>(plant).CycleDelay = TimeSpan.FromSeconds(0.25);

            var regularTray = SEntMan.SpawnAttachedTo("HydroponicsTray", new EntityCoordinates(greenhouse, 4.5f, -2.5f));
            regularPlant = SEntMan.SpawnAttachedTo("TomatoPlants", Coordinates(regularTray));
            SEntMan.System<PlantTraySystem>().PlantingPlantInTray(regularTray, regularPlant);
            SEntMan.GetComponent<PlantHolderComponent>(regularPlant).CycleDelay = TimeSpan.FromSeconds(0.1);
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<PlantHolderComponent>(plant).Age, Is.EqualTo(1));
            Assert.That(SEntMan.GetComponent<PlantHolderComponent>(regularPlant).Age, Is.GreaterThan(1));
            var cable = Find(greenhouse, "CableApcStack10");
            var gap = new EntityCoordinates(greenhouse, 7.5f, 4.5f);
            SEntMan.System<SharedTransformSystem>().SetCoordinates(worker, Coordinates(cable));
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(worker, cable), Is.True);
            Assert.That(SEntMan.System<SharedInteractionSystem>().InteractDoAfter(worker, cable, null, gap, true), Is.True);
            SEntMan.System<SharedTransformSystem>().SetCoordinates(worker, Coordinates(tray));
        });

        var ready = false;
        for (var i = 0; i < 45 && !ready; i++)
        {
            await Pair.RunSeconds(1);
            await Server.WaitPost(() => ready = SEntMan.GetComponent<PlantHolderComponent>(plant).ReadyForHarvest);
        }
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedPowerReceiverSystem>().IsPowered(tray), Is.True);
            Assert.That(fixtures.All(f => f.Comp2.Enabled), Is.True);
            Assert.That(ready, Is.True);
            Assert.That(SEntMan.System<PlantHarvestSystem>().TryHandleHarvest(plant, worker), Is.True);
            Assert.That(Find(greenhouse, "FoodTomato").Valid, Is.True);
        });
    }

    [Test]
    public async Task MineSmeltsLocalOreAfterDeliveryOfOutpostFuel()
    {
        EntityUid mine = default, generator = default, processor = default, rock = default, worker = default;
        await Server.WaitAssertion(() =>
        {
            mine = Site("orbitra-lavaland-ruin-mineshaft");
            generator = Find(mine, "PortableGeneratorJrPacman");
            processor = Find(mine, "OreProcessorIndustrial");
            rock = Find(mine, "GoldOre");
            var supply = Find(_outpost, "WeldingFuelTankFull");
            worker = SEntMan.SpawnAttachedTo("MobHuman", Coordinates(supply));
            Assert.That(SEntMan.System<GeneratorSystem>().GetFuel(generator), Is.Zero);
            Assert.That(SEntMan.System<MaterialStorageSystem>().TryInsertMaterialEntity(worker, rock, processor), Is.False);

            var beaker = SEntMan.SpawnAttachedTo("Beaker", Coordinates(supply));
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(worker, beaker), Is.True);
            var interaction = SEntMan.System<SharedInteractionSystem>();
            Assert.That(interaction.InteractUsing(worker, beaker, supply, Coordinates(supply)), Is.True);
            SEntMan.System<SharedTransformSystem>().SetCoordinates(worker, Coordinates(generator));
            Assert.That(interaction.InteractUsing(worker, beaker, generator, Coordinates(generator)), Is.True);
            Assert.That(SEntMan.System<GeneratorSystem>().GetFuel(generator), Is.Positive);
            SEntMan.System<GeneratorSystem>().SetFuelGeneratorOn(generator, true);
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedPowerReceiverSystem>().IsPowered(processor), Is.True);
            Assert.That(SEntMan.System<MaterialStorageSystem>().TryInsertMaterialEntity(worker, rock, processor), Is.True);
            var lathe = SEntMan.System<LatheSystem>();
            Assert.That(lathe.TryAddToQueue(processor, SProtoMan.Index(GoldIngot), 1), Is.True);
            Assert.That(lathe.TryStartProducing(processor), Is.True);
            Assert.That(Find(mine, "IngotGold1").Valid, Is.True);
        });
    }

    private EntityUid Site(string key)
    {
        var mapId = SEntMan.GetComponent<TransformComponent>(_surface).MapID;
        var name = Server.ResolveDependency<ILocalizationManager>().GetString(key);
        return SEntMan.System<SharedMapSystem>().GetAllGrids(mapId).ToList()
            .Single(grid => SEntMan.GetComponent<MetaDataComponent>(grid).EntityName == name);
    }

    private EntityCoordinates Coordinates(EntityUid entity) => SEntMan.GetComponent<TransformComponent>(entity).Coordinates;

    private EntityUid Find(EntityUid grid, string prototype)
    {
        var children = SEntMan.GetComponent<TransformComponent>(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (SEntMan.GetComponent<MetaDataComponent>(child).EntityPrototype?.ID == prototype)
                return child;
        }
        throw new AssertionException($"Не найден {prototype} на гриде {grid}");
    }
}
