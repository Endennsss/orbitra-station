// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Server._Orbitra.Xenobiology;
using Content.Shared._Orbitra.Xenobiology;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Orbitra.Xenobiology;

[TestFixture, NonParallelizable]
public sealed class OrbitraXenobiologyChemistryTest
{
    [TestCase("Silver", "SpaceCleaner", 40)]
    [TestCase("Blue", "Cryoxadone", 20)]
    [TestCase("Pyrite", "RobustHarvest", 30)]
    public async Task LiquidOutputFitsSampleBottle(string breed, string reagent, int amount)
    {
        await OrbitraXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            var solutions = server.System<SharedSolutionContainerSystem>();
            await server.WaitAssertion(() =>
            {
                var extract = server.EntMan.SpawnEntity($"OrbitraXenoExtract{breed}", lab.Coordinates);
                Assert.That(solutions.TryGetSolution(extract, "extract", out var solution, out _), Is.True);
                Assert.That(solutions.TryAddReagent(solution!.Value, "Water", FixedPoint2.New(5)), Is.True);
                Assert.That(lab.CountPrototype("OrbitraXenoSampleBottle"), Is.EqualTo(1));
                var query = server.EntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
                while (query.MoveNext(out var uid, out var metadata, out var transform))
                {
                    if (transform.MapUid != lab.Map || metadata.EntityPrototype?.ID != "OrbitraXenoSampleBottle")
                        continue;
                    Assert.That(solutions.TryGetSolution(uid, "drink", out _, out var output), Is.True);
                    Assert.That(output!.GetTotalPrototypeQuantity(reagent), Is.EqualTo(FixedPoint2.New(amount)));
                }
            });
        });
    }

    /// <summary>Nested chemistry callbacks and excess catalyst cannot duplicate finite material outputs.</summary>
    [Test]
    public async Task ExtractReactsOnceWithFixedYieldThroughNativeSolutionEvents()
    {
        await OrbitraXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid extract = default;
            var added = false;
            var repeated = true;
            await server.WaitPost(() =>
            {
                extract = server.EntMan.SpawnEntity("OrbitraXenoExtractMetal", lab.Coordinates);
                var solutions = server.System<SharedSolutionContainerSystem>();
                if (solutions.TryGetSolution(extract, "extract", out var solution, out _))
                {
                    added = solutions.TryAddReagent(solution.Value, "Water", FixedPoint2.New(15));
                    repeated = server.System<OrbitraXenobiologyExtractSystem>().TryReact((extract,
                        server.EntMan.GetComponent<OrbitraXenobiologyExtractComponent>(extract)));
                    solutions.TryAddReagent(solution.Value, "Water", FixedPoint2.New(1));
                }
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(added, Is.True);
                Assert.That(repeated, Is.False);
                Assert.That(server.EntMan.GetComponent<OrbitraXenobiologyExtractComponent>(extract).Used, Is.True);
                Assert.That(lab.CountPrototype("SheetSteel1"), Is.EqualTo(1));
                Assert.That(lab.CountPrototype("SheetGlass1"), Is.EqualTo(1));
                var query = server.EntMan.EntityQueryEnumerator<StackComponent, MetaDataComponent, TransformComponent>();
                while (query.MoveNext(out var stack, out var metadata, out var transform))
                {
                    if (transform.MapUid != lab.Map)
                        continue;
                    if (metadata.EntityPrototype?.ID == "SheetSteel1")
                        Assert.That(stack.Count, Is.EqualTo(5));
                    if (metadata.EntityPrototype?.ID == "SheetGlass1")
                        Assert.That(stack.Count, Is.EqualTo(3));
                }
            });
        });
    }

    /// <summary>Hydration inside a hand container remains dormant until the native drop event.</summary>
    [Test]
    public async Task HydratedCultureHatchesOnlyAfterBeingDropped()
    {
        await OrbitraXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid culture = default;
            var pickedUp = false;
            var dropped = false;
            await server.WaitPost(() =>
            {
                culture = server.EntMan.SpawnEntity("OrbitraXenoCultureGrey", lab.Coordinates);
                pickedUp = server.System<SharedHandsSystem>().TryPickupAnyHand(lab.User, culture);
                var solutions = server.System<SharedSolutionContainerSystem>();
                if (solutions.TryGetSolution(culture, "culture", out var solution, out _))
                    solutions.TryAddReagent(solution.Value, "Water", FixedPoint2.New(5));
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(pickedUp, Is.True);
                Assert.That(lab.Specimens(), Has.Count.EqualTo(1));
                Assert.That(server.EntMan.GetComponent<OrbitraXenobiologyCultureComponent>(culture).Used, Is.False);
            });
            await server.WaitPost(() => dropped = server.System<SharedHandsSystem>().TryDrop((lab.User, null), culture));
            await server.WaitRunTicks(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(dropped, Is.True);
                Assert.That(lab.Specimens(), Has.Count.EqualTo(2));
                Assert.That(server.EntMan.EntityExists(culture), Is.False);
            });
        });
    }

    /// <summary>Hydrated cultures cannot create creatures on empty space outside the laboratory floor.</summary>
    [Test]
    public async Task CultureRequiresARealFloorTile()
    {
        await OrbitraXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid culture = default;
            var hatched = true;
            await server.WaitPost(() =>
            {
                culture = server.EntMan.SpawnEntity("OrbitraXenoCultureGrey",
                    new EntityCoordinates(lab.Coordinates.EntityId, 20, 20));
                var solutions = server.System<SharedSolutionContainerSystem>();
                if (solutions.TryGetSolution(culture, "culture", out var solution, out _))
                    solutions.TryAddReagent(solution.Value, "Water", FixedPoint2.New(5));
                hatched = server.System<OrbitraXenobiologyExtractSystem>().TryHatch((culture,
                    server.EntMan.GetComponent<OrbitraXenobiologyCultureComponent>(culture)));
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(hatched, Is.False);
                Assert.That(lab.Specimens(), Has.Count.EqualTo(1));
                Assert.That(server.EntMan.GetComponent<OrbitraXenobiologyCultureComponent>(culture).Used, Is.False);
            });
        });
    }

    /// <summary>A capped trait leaves the serum intact; a successful treatment spends it exactly once.</summary>
    [Test]
    public async Task CappedSerumIsNotConsumed()
    {
        await OrbitraXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid serum = default;
            var equipped = false;
            var capped = true;
            var applied = false;
            var repeated = true;
            await server.WaitPost(() =>
            {
                serum = server.EntMan.SpawnEntity("OrbitraXenoSerumDocility", lab.Coordinates);
                equipped = server.System<SharedHandsSystem>().TryPickupAnyHand(lab.User, serum);
                lab.State.Docility = 1;
                capped = server.System<OrbitraXenobiologyExtractSystem>().TryApplySerum((serum,
                    server.EntMan.GetComponent<OrbitraXenobiologySerumComponent>(serum)), lab.User, lab.Slime);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(equipped, Is.True);
                Assert.That(capped, Is.False);
                Assert.That(server.EntMan.GetComponent<OrbitraXenobiologySerumComponent>(serum).Used, Is.False);
                Assert.That(server.System<SharedHandsSystem>().IsHolding(lab.User, serum), Is.True);
            });
            await server.WaitPost(() =>
            {
                lab.State.Docility = 0.5f;
                var item = server.EntMan.GetComponent<OrbitraXenobiologySerumComponent>(serum);
                applied = server.System<OrbitraXenobiologyExtractSystem>().TryApplySerum((serum, item), lab.User, lab.Slime);
                repeated = server.System<OrbitraXenobiologyExtractSystem>().TryApplySerum((serum, item), lab.User, lab.Slime);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(applied, Is.True);
                Assert.That(repeated, Is.False);
                Assert.That(lab.State.Docility, Is.EqualTo(0.75f));
            });
        });
    }
}
