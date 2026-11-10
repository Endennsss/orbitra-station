// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.Xenobiology;
using Content.Client._Orbitra.Stylesheets;
using Content.Client._Orbitra.UserInterface;
using Content.IntegrationTests.Pair;
using Content.Server._Orbitra.Xenobiology;
using Content.Server.Power.EntitySystems;
using Content.Shared._Orbitra.Xenobiology;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra.Xenobiology;

[TestFixture, NonParallelizable]
public sealed class OrbitraXenobiologyUiTest
{
    /// <summary>A transfer updates an already open processor before the next periodic UI refresh.</summary>
    [Test]
    public async Task ProcessorTransferImmediatelyEnablesProcessingInOpenUi()
    {
        await WithProcessor(async context =>
        {
            var server = context.Pair.Server;
            await server.WaitAssertion(() =>
            {
                var state = ProcessorState(context);
                Assert.That(state.Powered, Is.True);
                Assert.That(state.Status, Is.EqualTo(OrbitraXenobiologyProcessorStatus.Empty));
                Assert.That(state.CanProcess, Is.False);
            });
            await Transfer(context);
            await server.WaitAssertion(() =>
            {
                var state = ProcessorState(context);
                Assert.That(state.CanProcess, Is.True,
                    "The open processor must show the transferred corpse without waiting for another simulation tick.");
                Assert.That(state.Status, Is.EqualTo(OrbitraXenobiologyProcessorStatus.Ready));
                Assert.That(state.HasInput, Is.True);
                Assert.That(state.CanEject, Is.True);
                Assert.That(state.Input?.Entity, Is.EqualTo(server.EntMan.GetNetEntity(context.Corpse)));
                Assert.That(state.ExpectedYield, Is.EqualTo(1));
            });
        });
    }

    /// <summary>Validated process and eject messages refresh snapshots without a polling delay.</summary>
    [Test]
    public async Task ProcessAndEjectImmediatelyRefreshOpenUi()
    {
        await WithProcessor(async context =>
        {
            var server = context.Pair.Server;
            await Transfer(context);
            await server.WaitPost(() => Send(context, new OrbitraXenobiologyProcessMessage()));
            await server.WaitAssertion(() =>
            {
                var state = ProcessorState(context);
                Assert.That(state.Status, Is.EqualTo(OrbitraXenobiologyProcessorStatus.Processing));
                Assert.That(state.Processing, Is.True);
                Assert.That(state.CanProcess, Is.False);
                Assert.That(state.CanEject, Is.True);
                Assert.That(state.SecondsRemaining, Is.GreaterThan(0));
                Assert.That(state.Duration, Is.EqualTo(5f));
            });
            await server.WaitPost(() => Send(context, new OrbitraXenobiologyEjectMessage()));
            await server.WaitAssertion(() =>
            {
                AssertEmpty(ProcessorState(context));
                Assert.That(server.EntMan.EntityExists(context.Corpse), Is.True);
                Assert.That(server.EntMan.GetComponent<OrbitraXenobiologySlimeComponent>(context.Corpse).Consumed, Is.False);
                Assert.That(server.System<SharedContainerSystem>().IsEntityInContainer(context.Corpse), Is.False);
            });
        });
    }

    /// <summary>Both normal ejection and external container removal permit immediate transfer and restart.</summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task ProcessingCanRestartInSameTickAfterEjectAndTransfer(bool useEject)
    {
        await WithProcessor(async context =>
        {
            var server = context.Pair.Server;
            var devices = server.System<OrbitraXenobiologyDeviceSystem>();
            var inserted = false;
            var transferred = false;
            var started = false;
            var initiallyStarted = false;
            var ejected = false;
            await Transfer(context);
            await server.WaitPost(() =>
            {
                Entity<OrbitraXenobiologyProcessorComponent> processor =
                    (context.Processor, server.EntMan.GetComponent<OrbitraXenobiologyProcessorComponent>(context.Processor));
                Send(context, new OrbitraXenobiologyProcessMessage());
                initiallyStarted = server.EntMan.HasComponent<OrbitraXenobiologyProcessingComponent>(context.Processor);
                var containers = server.System<SharedContainerSystem>();
                if (useEject)
                {
                    Send(context, new OrbitraXenobiologyEjectMessage());
                    ejected = !containers.IsEntityInContainer(context.Corpse);
                }
                else
                {
                    containers.TryGetContainer(context.Processor, processor.Comp.Container, out var input);
                    ejected = containers.Remove(context.Corpse, input!,
                        destination: server.EntMan.GetComponent<TransformComponent>(context.User).Coordinates);
                }
                var contents = containers.EnsureContainer<Robust.Shared.Containers.Container>(context.Carrier, "xeno_carrier");
                inserted = containers.Insert(context.Corpse, contents);
                transferred = devices.TryTransfer(
                    (context.Carrier, server.EntMan.GetComponent<OrbitraXenobiologyCarrierComponent>(context.Carrier)),
                    context.User, processor);
                started = devices.TryProcess(processor, context.User);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(initiallyStarted, Is.True);
                Assert.That(ejected, Is.True);
                Assert.That(inserted, Is.True);
                Assert.That(transferred, Is.True, "A cancelled processing marker must not block insertion until a periodic update.");
                Assert.That(started, Is.True, "The reinserted corpse must be immediately processable after cancellation.");
                Assert.That(ProcessorState(context).Processing, Is.True);
                Assert.That(server.EntMan.GetComponent<OrbitraXenobiologySlimeComponent>(context.Corpse).Consumed, Is.False);
            });
            await context.Pair.RunTicksSync(3);
            await server.WaitAssertion(() =>
            {
                Assert.That(ProcessorState(context).Processing, Is.True,
                    "Deferred removal of the cancelled marker must not stop the newly started batch.");
                Assert.That(server.EntMan.GetComponent<OrbitraXenobiologySlimeComponent>(context.Corpse).Consumed, Is.False);
            });
        });
    }

    /// <summary>The completion snapshot must not retain the spent corpse until the next UI poll.</summary>
    [Test]
    public async Task ProcessingCompletionImmediatelyClearsSpentInput()
    {
        await WithProcessor(async context =>
        {
            var server = context.Pair.Server;
            await Transfer(context);
            await server.WaitPost(() =>
            {
                server.EntMan.GetComponent<OrbitraXenobiologyProcessorComponent>(context.Processor).Duration = 1f;
                Send(context, new OrbitraXenobiologyProcessMessage());
            });
            var processing = true;
            for (var tick = 0; tick < 180 && processing; tick++)
            {
                await context.Pair.RunTicksSync(1);
                await server.WaitPost(() => processing =
                    server.EntMan.HasComponent<OrbitraXenobiologyProcessingComponent>(context.Processor));
            }
            await server.WaitAssertion(() =>
            {
                Assert.That(processing, Is.False, "A one-second batch must complete within three seconds.");
                AssertEmpty(ProcessorState(context));
                Assert.That(server.EntMan.EntityExists(context.Corpse), Is.False);
                Assert.That(server.EntMan.EntityQueryEnumerator<OrbitraXenobiologyExtractComponent>().MoveNext(out _, out _), Is.True);
            });
        });
    }

    /// <summary>Power failure stops the operation while preserving an ejectable, unspent corpse.</summary>
    [Test]
    public async Task PowerLossPublishesStoppedInputAndAllowsEjection()
    {
        await WithProcessor(async context =>
        {
            var server = context.Pair.Server;
            await Transfer(context);
            await server.WaitPost(() =>
            {
                Send(context, new OrbitraXenobiologyProcessMessage());
                server.System<PowerReceiverSystem>().SetNeedsPower(context.Processor, true);
            });
            await context.Pair.RunTicksSync(3);
            await server.WaitAssertion(() =>
            {
                var state = ProcessorState(context);
                Assert.That(state.Status, Is.EqualTo(OrbitraXenobiologyProcessorStatus.NoPower));
                Assert.That(state.Powered, Is.False);
                Assert.That(state.Processing, Is.False);
                Assert.That(state.CanProcess, Is.False);
                Assert.That(state.HasInput, Is.True);
                Assert.That(state.CanEject, Is.True);
                Assert.That(state.Input?.Entity, Is.EqualTo(server.EntMan.GetNetEntity(context.Corpse)));
                Assert.That(server.EntMan.GetComponent<OrbitraXenobiologySlimeComponent>(context.Corpse).Consumed, Is.False);
            });
            await server.WaitPost(() => Send(context, new OrbitraXenobiologyEjectMessage()));
            await server.WaitAssertion(() =>
            {
                var state = ProcessorState(context);
                Assert.That(state.HasInput, Is.False);
                Assert.That(state.Input, Is.Null);
                Assert.That(state.CanEject, Is.False);
            });
        });
    }

    /// <summary>The processor uses its own compact window, with buttons wired through the native BUI.</summary>
    [Test]
    public async Task ProcessorOpensSeparateWindowAndButtonsOperateOnInput()
    {
        await WithProcessor(async context =>
        {
            var pair = context.Pair;
            var client = pair.Client;
            var loc = client.ResolveDependency<ILocalizationManager>();
            OrbitraXenobiologyProcessorWindow window = null!;
            await client.WaitPost(() => window = client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
                .OfType<OrbitraXenobiologyProcessorWindow>().Single());
            await client.WaitAssertion(() =>
            {
                Assert.That(client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
                    .OfType<OrbitraXenobiologyWindow>(), Is.Empty);
                Assert.That(window.ProcessButton.Disabled, Is.True);
                Assert.That(window.EjectButton.Disabled, Is.True);
                Assert.That(window.EmptyLabel.Visible, Is.True);
                Assert.That(window.Size.X, Is.LessThan(700f));
            });
            await Transfer(context);
            await pair.RunTicksSync(15);
            await client.WaitAssertion(() =>
            {
                Assert.That(window.ProcessButton.Disabled, Is.False);
                Assert.That(window.EjectButton.Disabled, Is.False);
                Assert.That(window.InputName.Text, Is.Not.Null.And.Not.Empty);
                Assert.That(window.ActionHint.Visible, Is.True);
                Assert.That(window.EjectButton.Text, Is.EqualTo(loc.GetString("orbitra-xeno-eject")));
            });
            await Click(pair, window.ProcessButton);
            await pair.RunTicksSync(15);
            await context.Pair.Server.WaitAssertion(() =>
                Assert.That(ProcessorState(context).Processing, Is.True));
            await client.WaitAssertion(() =>
            {
                Assert.That(window.ProcessButton.Disabled, Is.True);
                Assert.That(window.EjectButton.Disabled, Is.False);
                Assert.That(window.ActionHint.Visible, Is.True);
                Assert.That(window.EjectButton.Text, Is.EqualTo(loc.GetString("orbitra-xeno-cancel-eject")));
            });
            await Click(pair, window.EjectButton);
            await pair.RunTicksSync(15);
            await context.Pair.Server.WaitAssertion(() => AssertEmpty(ProcessorState(context)));
            await client.WaitAssertion(() =>
            {
                Assert.That(window.EmptyLabel.Visible, Is.True);
                Assert.That(window.ProcessButton.Disabled, Is.True);
                Assert.That(window.EjectButton.Disabled, Is.True);
                Assert.That(window.ActionHint.Visible, Is.False);
            });
        }, connected: true);
    }

    /// <summary>Individual selection follows identity across polling, reordering and temporary searches.</summary>
    [Test]
    public async Task ConsoleKeepsIndividualSelectionAndRowsAcrossRefreshAndSearch()
    {
        await WithConsoleWindow(async (pair, window) =>
        {
            var first = Specimen(101, "001");
            var second = Specimen(102, "002");
            second.Name = "[bold]Specimen name[/bold]";
            var selected = false;
            Control originalRow = null!;
            await pair.Client.WaitPost(() =>
            {
                window.UpdateState(new OrbitraXenobiologyBoundUserInterfaceState { Powered = true, Specimens = [first, second] });
                selected = window.SelectSpecimen(second.Entity);
                originalRow = window.SpecimenList.Children.Single(child =>
                    Descendants(child).OfType<Label>().Any(label => label.Text?.Contains(second.Identifier) == true));
            });
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(selected, Is.True);
                Assert.That(window.SelectedSpecimen, Is.EqualTo(second.Entity));
                Assert.That(window.SelectedName.Text, Does.Contain(second.Name), "Entity names must remain literal plain text.");
            });
            second.Growth = 0.75f;
            await pair.Client.WaitPost(() => window.UpdateState(new OrbitraXenobiologyBoundUserInterfaceState
                { Powered = true, Specimens = [second, first] }));
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(window.SelectedSpecimen, Is.EqualTo(second.Entity));
                Assert.That(window.SpecimenList.Children, Does.Contain(originalRow), "Polling should reuse the specimen's existing row.");
            });
            await pair.Client.WaitPost(() => window.SpecimenSearch.SetText(second.Identifier, invokeEvent: true));
            await pair.Client.WaitAssertion(() => Assert.That(window.SpecimenList.Children.Count(child => child.Visible), Is.EqualTo(1)));
            await pair.Client.WaitPost(() => window.SpecimenSearch.SetText("no-such-specimen-xyz", invokeEvent: true));
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(window.SpecimenList.Children.Count(child => child.Visible), Is.Zero);
                Assert.That(window.ColonyEmpty.Visible, Is.True);
                Assert.That(window.SelectedSpecimen, Is.EqualTo(second.Entity));
            });
            await pair.Client.WaitPost(() =>
            {
                window.SpecimenSearch.SetText(string.Empty, invokeEvent: true);
                window.UpdateState(new OrbitraXenobiologyBoundUserInterfaceState { Powered = true, Specimens = [first, second] });
            });
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(window.SpecimenList.Children.Count(child => child.Visible), Is.EqualTo(2));
                Assert.That(window.SelectedSpecimen, Is.EqualTo(second.Entity));
                Assert.That(window.SpecimenList.Children, Does.Contain(originalRow));
            });
        });
    }

    /// <summary>Live colony updates leave the selected reference and its controls intact.</summary>
    [Test]
    public async Task CatalogSelectionAndDetailsSurviveColonyUpdatesAndEmptySearch()
    {
        await WithConsoleWindow(async (pair, window) =>
        {
            var selected = false;
            Control[] details = [];
            await pair.Client.WaitPost(() =>
            {
                selected = window.SelectBreed("OrbitraBlue");
                details = window.BreedDetails.Children.ToArray();
                window.UpdateState(new OrbitraXenobiologyBoundUserInterfaceState { Powered = true, Specimens = [Specimen(201, "201")] });
                window.CatalogSearch.SetText("no-such-lineage-xyz", invokeEvent: true);
            });
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(selected, Is.True);
                Assert.That(window.SelectedBreed, Is.EqualTo("OrbitraBlue"));
                Assert.That(window.CatalogEmpty.Visible, Is.True);
                Assert.That(window.BreedDetails.Children, Is.EqualTo(details));
            });
            await pair.Client.WaitPost(() => window.CatalogSearch.SetText(string.Empty, invokeEvent: true));
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(window.CatalogEmpty.Visible, Is.False);
                Assert.That(window.SelectedBreed, Is.EqualTo("OrbitraBlue"));
                Assert.That(window.BreedDetails.Children, Is.EqualTo(details));
            });
        });
    }

    /// <summary>Read-only measurements preserve the recipe page, scroll position and active search input.</summary>
    [Test]
    public async Task CatalogRecipePageKeepsScrollAndFocusAcrossMeasurementsAndPageChanges()
    {
        await WithConsoleWindow(async (pair, window) =>
        {
            var viewport = new Vector2(1000, 450);
            var scroll = window.FindControl<ScrollContainer>("CatalogRecipesScroll");
            var care = window.FindControl<ScrollContainer>("CatalogDetailsScroll");
            var recipesButton = window.FindControl<Button>("CatalogRecipesButton");
            var careButton = window.FindControl<Button>("CatalogCareButton");
            Control[] recipes = [];
            var position = Vector2.Zero;
            await Click(pair, window.FindControl<Button>("CatalogButton"));
            await pair.Client.WaitPost(() =>
            {
                window.FitToViewport(viewport);
                window.SelectBreed("OrbitraGrey");
                ArrangeWindow(window, viewport);
            });
            await Click(pair, recipesButton);
            await pair.Client.WaitPost(() =>
            {
                ArrangeWindow(window, viewport);
                scroll.SetScrollValue(new Vector2(0, 80));
                position = scroll.GetScrollValue();
                recipes = window.BreedRecipes.Children.ToArray();
                window.CatalogSearch.GrabKeyboardFocus();
                window.UpdateState(new OrbitraXenobiologyBoundUserInterfaceState { Powered = true, Specimens = [Specimen(202, "202")] });
                ArrangeWindow(window, viewport);
            });
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(position.Y, Is.GreaterThan(0), "The recipe reference must exceed the compact viewport for this regression.");
                Assert.That(window.SelectedBreed, Is.EqualTo("OrbitraGrey"));
                Assert.That(scroll.VisibleInTree, Is.True);
                Assert.That(care.VisibleInTree, Is.False);
                Assert.That(scroll.GetScrollValue(), Is.EqualTo(position));
                Assert.That(window.BreedRecipes.Children, Is.EqualTo(recipes));
                Assert.That(pair.Client.ResolveDependency<IUserInterfaceManager>().KeyboardFocused, Is.SameAs(window.CatalogSearch));
            });
            await Click(pair, careButton);
            await pair.Client.WaitPost(() => ArrangeWindow(window, viewport));
            await Click(pair, recipesButton);
            await pair.Client.WaitPost(() => ArrangeWindow(window, viewport));
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(scroll.GetScrollValue(), Is.EqualTo(position));
                Assert.That(window.BreedRecipes.Children, Is.EqualTo(recipes));
            });
        });
    }

    /// <summary>A mutation target can be opened even when a tier filter conceals it.</summary>
    [Test]
    public async Task CatalogNavigationRevealsBreedHiddenByTierFilter()
    {
        await WithConsoleWindow(async (pair, window) =>
        {
            var loc = pair.Client.ResolveDependency<ILocalizationManager>();
            Button option = null!;
            var selected = false;
            await Click(pair, window.FindControl<Button>("CatalogButton"));
            await Click(pair, window.CatalogTierFilter);
            await pair.Client.WaitPost(() => option = Descendants(window.CatalogTierFilter.OptionsScroll).OfType<Button>()
                .Single(button => button.Text == loc.GetString("orbitra-xeno-tier", ("tier", 0))));
            await Click(pair, option);
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(window.CatalogTierFilter.SelectedId, Is.EqualTo(0));
                Assert.That(window.BreedList.Children.Count(child => child.Visible), Is.EqualTo(1));
            });
            await pair.Client.WaitPost(() => selected = window.SelectBreed("OrbitraBlue"));
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(selected, Is.True);
                Assert.That(window.SelectedBreed, Is.EqualTo("OrbitraBlue"));
                Assert.That(window.CatalogTierFilter.SelectedId, Is.EqualTo(-1));
                Assert.That(window.BreedList.Children.Count(child => child.Visible), Is.GreaterThan(1));
                Assert.That(window.BreedList.Children.OfType<Button>().Single(button => button.Pressed).Visible, Is.True);
            });
        });
    }

    /// <summary>Care indicators distinguish low temperature from containment and a dead specimen.</summary>
    [Test]
    public async Task ConsoleShowsColdContainedAndDeadStatusesWithComfortRange()
    {
        await WithConsoleWindow(async (pair, window) =>
        {
            var specimen = Specimen(301, "301");
            specimen.Temperature = 273.15f;
            var loc = pair.Client.ResolveDependency<ILocalizationManager>();
            foreach (var (suspended, dead, status) in new[]
                     {
                         (false, false, "orbitra-xeno-cold"),
                         (true, false, "orbitra-xeno-contained"),
                         (true, true, "orbitra-xeno-dead"),
                     })
            {
                await pair.Client.WaitPost(() =>
                {
                    specimen.Suspended = suspended;
                    specimen.Dead = dead;
                    window.UpdateState(new OrbitraXenobiologyBoundUserInterfaceState { Powered = true, Specimens = [specimen] });
                });
                await pair.Client.WaitAssertion(() =>
                {
                    var labels = Descendants(window.SpecimenDetails).OfType<Label>().Select(label => label.Text).ToArray();
                    Assert.That(labels, Does.Contain(loc.GetString(status)));
                    Assert.That(labels, Does.Contain(loc.GetString("orbitra-xeno-temperature-value", ("temperature", "0"))));
                    Assert.That(labels, Does.Contain(loc.GetString("orbitra-xeno-temperature-range", ("minimum", "10"), ("maximum", "40"))));
                    Assert.That(window.SelectedSpecimen, Is.EqualTo(specimen.Entity));
                });
            }
        });
    }

    /// <summary>Wide and small viewports retain usable controls on populated and empty device pages.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task WindowsFitSmallViewportWithoutClippingPrimaryControls(bool processor)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.RunTicksSync(15);
        Control window = null!;
        try
        {
            foreach (var viewport in new[] { new Vector2(1100, 760), new Vector2(640, 360) })
            {
                await pair.Client.WaitPost(() =>
                {
                    if (window != null)
                        window.Parent?.RemoveChild(window);
                    if (processor)
                    {
                        var createdMachine = new OrbitraXenobiologyProcessorWindow();
                        createdMachine.OpenCentered();
                        window = createdMachine;
                        createdMachine.FitToViewport(viewport);
                    }
                    else
                    {
                        var createdConsole = new OrbitraXenobiologyWindow();
                        createdConsole.OpenCentered();
                        window = createdConsole;
                        createdConsole.FitToViewport(viewport);
                    }
                });
                if (window is OrbitraXenobiologyProcessorWindow machine)
                {
                    var input = Specimen(401, "401");
                    input.Name = new string('Ж', 200);
                    input.Dead = true;
                    foreach (var status in new[] { OrbitraXenobiologyProcessorStatus.Empty, OrbitraXenobiologyProcessorStatus.NoPower,
                                 OrbitraXenobiologyProcessorStatus.Ready, OrbitraXenobiologyProcessorStatus.Processing })
                    {
                        var loaded = status is OrbitraXenobiologyProcessorStatus.Ready or OrbitraXenobiologyProcessorStatus.Processing;
                        var processing = status == OrbitraXenobiologyProcessorStatus.Processing;
                        await pair.Client.WaitPost(() =>
                        {
                            machine.UpdateState(new OrbitraXenobiologyProcessorBoundUserInterfaceState
                            {
                                Status = status, Powered = status != OrbitraXenobiologyProcessorStatus.NoPower,
                                HasInput = loaded, Input = loaded ? input : null, Processing = processing,
                                CanProcess = status == OrbitraXenobiologyProcessorStatus.Ready, CanEject = loaded,
                                ExpectedYield = loaded ? 3 : 0, Duration = 5, SecondsRemaining = processing ? 2.5f : 0,
                                Progress = processing ? 0.5f : 0,
                            });
                            ArrangeWindow(window, viewport);
                        });
                        await pair.Client.WaitAssertion(() =>
                        {
                            AssertViewport(window, viewport);
                            AssertInside(window, machine.ProcessButton);
                            AssertInside(window, machine.EjectButton);
                            AssertInside(window, machine.Status);
                            Assert.That(machine.InputPanel.Visible, Is.EqualTo(loaded));
                            Assert.That(machine.EmptyLabel.Visible, Is.EqualTo(!loaded));
                            Assert.That(machine.ProcessButton.Disabled, Is.EqualTo(status != OrbitraXenobiologyProcessorStatus.Ready));
                            Assert.That(machine.EjectButton.Disabled, Is.EqualTo(!loaded));
                            if (!loaded)
                                return;
                            var scroll = Descendants(machine).OfType<ScrollContainer>().Single();
                            Assert.That(scroll.Width, Is.GreaterThan(0));
                            Assert.That(scroll.Height, Is.GreaterThan(0));
                            AssertHorizontalInside(scroll, machine.InputPanel, viewport);
                            AssertHorizontalInside(scroll, machine.InputName, viewport);
                            Assert.That(machine.Progress.VisibleInTree, Is.EqualTo(processing));
                            if (processing)
                                AssertHorizontalInside(scroll, machine.Progress, viewport);
                        });
                    }
                }
                else if (window is OrbitraXenobiologyWindow console)
                {
                    foreach (var powered in new[] { true, false })
                    {
                        await pair.Client.WaitPost(() =>
                        {
                            console.UpdateState(new OrbitraXenobiologyBoundUserInterfaceState { Powered = powered });
                            ArrangeWindow(window, viewport);
                        });
                        await pair.Client.WaitAssertion(() =>
                        {
                            AssertViewport(window, viewport);
                            AssertInside(window, console.SpecimenSearch);
                            AssertInside(window, console.Status);
                        });
                    }
                    var selected = false;
                    var specimen = Specimen(402, "402");
                    specimen.Name = new string('Ж', 200);
                    await pair.Client.WaitPost(() =>
                    {
                        console.UpdateState(new OrbitraXenobiologyBoundUserInterfaceState { Powered = true, Specimens = [specimen] });
                        selected = console.SelectSpecimen(specimen.Entity);
                        ArrangeWindow(window, viewport);
                    });
                    await pair.Client.WaitAssertion(() =>
                    {
                        Assert.That(selected, Is.True);
                        AssertViewport(window, viewport);
                        Assert.That(console.SpecimenDetails.VisibleInTree, Is.True);
                        AssertInside(window, console.Status);
                        var scroll = console.FindControl<ScrollContainer>("SpecimenDetailsScroll");
                        AssertInside(window, scroll);
                        Assert.That(scroll.Height, Is.GreaterThan(0));
                        AssertHorizontalInside(scroll, console.SpecimenDetails, viewport);
                        AssertHorizontalInside(scroll, console.SelectedName, viewport);
                        Assert.That(console.SpecimenSearch.VisibleInTree, Is.EqualTo(viewport.X >= 760));
                        if (viewport.X < 760)
                            AssertInside(window, console.FindControl<Button>("SpecimenBack"));
                    });
                    await Click(pair, console.FindControl<Button>("CatalogButton"));
                    await pair.Client.WaitPost(() =>
                    {
                        selected = console.SelectBreed("OrbitraBluespace");
                        ArrangeWindow(window, viewport);
                    });
                    await pair.Client.WaitAssertion(() =>
                    {
                        Assert.That(selected, Is.True);
                        AssertViewport(window, viewport);
                        Assert.That(console.BreedDetails.VisibleInTree, Is.True);
                        var scroll = console.FindControl<ScrollContainer>("CatalogDetailsScroll");
                        AssertInside(window, scroll);
                        Assert.That(scroll.Height, Is.GreaterThan(0));
                        AssertHorizontalInside(scroll, console.BreedDetails, viewport);
                        Assert.That(console.CatalogSearch.VisibleInTree, Is.EqualTo(viewport.X >= 760));
                        if (viewport.X < 760)
                            AssertInside(window, console.FindControl<Button>("CatalogBack"));
                    });
                    await Click(pair, console.FindControl<Button>("CatalogRecipesButton"));
                    await pair.Client.WaitPost(() => ArrangeWindow(window, viewport));
                    await pair.Client.WaitAssertion(() =>
                    {
                        var scroll = console.FindControl<ScrollContainer>("CatalogRecipesScroll");
                        Assert.That(scroll.VisibleInTree, Is.True);
                        AssertInside(window, scroll);
                        AssertInside(window, console.FindControl<Label>("CatalogName"));
                        AssertInside(window, console.FindControl<Button>("CatalogCareButton"));
                        AssertInside(window, console.FindControl<Button>("CatalogRecipesButton"));
                        Assert.That(scroll.Height, Is.GreaterThan(0));
                        AssertHorizontalInside(scroll, console.BreedRecipes, viewport);
                        foreach (var recipe in Descendants(console.BreedRecipes).OfType<RichTextLabel>())
                            AssertHorizontalInside(scroll, recipe, viewport);
                    });
                    await Click(pair, console.FindControl<Button>("CatalogCareButton"));
                    await pair.Client.WaitPost(() => ArrangeWindow(window, viewport));
                    if (viewport.X < 760)
                    {
                        var guideButton = console.FindControl<Button>("GuideButton");
                        var guide = console.FindControl<ScrollContainer>("HelpPanel");
                        await Click(pair, guideButton);
                        await pair.Client.WaitPost(() => ArrangeWindow(window, viewport));
                        await pair.Client.WaitAssertion(() =>
                        {
                            Assert.That(guide.VisibleInTree, Is.True);
                            Assert.That(console.BreedDetails.VisibleInTree, Is.False);
                            AssertInside(window, guide);
                            AssertInside(window, guideButton);
                            Assert.That(guide.Height, Is.GreaterThan(0));
                            AssertHorizontalInside(guide, Descendants(guide).OfType<RichTextLabel>().Single(), viewport);
                        });
                        await Click(pair, guideButton);
                        await pair.Client.WaitPost(() => ArrangeWindow(window, viewport));
                        await pair.Client.WaitAssertion(() =>
                        {
                            Assert.That(guide.VisibleInTree, Is.False);
                            Assert.That(console.BreedDetails.VisibleInTree, Is.True);
                            Assert.That(console.SelectedBreed, Is.EqualTo("OrbitraBluespace"));
                            Assert.That(console.SelectedSpecimen, Is.EqualTo(specimen.Entity));
                        });
                    }
                }
            }
        }
        finally
        {
            await pair.Client.WaitPost(() => window?.Parent?.RemoveChild(window));
        }
        await pair.CleanReturnAsync();
    }

    /// <summary>Shared chrome and local theme also cover controls created after the windows open.</summary>
    [Test]
    public async Task WindowsAndDynamicCatalogUseSharedOrbitraTheme()
    {
        await WithConsoleWindow(async (pair, window) =>
        {
            OrbitraXenobiologyProcessorWindow processor = null!;
            try
            {
                await pair.Client.WaitPost(() =>
                {
                    processor = new OrbitraXenobiologyProcessorWindow();
                    processor.OpenCentered();
                    window.UpdateState(new OrbitraXenobiologyBoundUserInterfaceState
                    { Powered = true, Range = 10, Specimens = [Specimen(101, "001")] });
                    Assert.That(window.SelectBreed("OrbitraBlue"), Is.True);
                });
                await pair.RunTicksSync(2);
                await pair.Client.WaitAssertion(() =>
                {
                    foreach (var themedWindow in new Control[] { window, processor })
                    {
                        Assert.That(themedWindow.HasStyleClass("OrbitraEntryWindow"), Is.True);
                        Assert.That(themedWindow.HasStyleClass("OrbitraWindowChrome"), Is.True);
                        var panels = Descendants(themedWindow).OfType<PanelContainer>().Where(panel =>
                            panel.HasStyleClass("OrbitraWindowSurface") || panel.HasStyleClass("OrbitraWindowHeader")).ToArray();
                        Assert.That(panels, Is.Not.Empty);
                        foreach (var panel in panels)
                        {
                            Assert.That(panel.PanelOverride, Is.Null, panel.Name);
                            Assert.That(panel.TryGetStyleProperty<StyleBox>(PanelContainer.StylePropertyPanel, out var box), Is.True);
                            Assert.That(box, Is.TypeOf<StyleBoxFlat>());
                            Assert.That(((StyleBoxFlat) box).BackgroundColor,
                                Is.EqualTo(panel.HasStyleClass("OrbitraWindowHeader")
                                    ? OrbitraPalettes.PanelBackground : OrbitraPalettes.PanelInset), panel.Name);
                        }
                        foreach (var scroll in Descendants(themedWindow).OfType<ScrollBar>())
                        {
                            Assert.That(scroll.HasStyleClass("OrbitraEditorControl"), Is.True);
                            Assert.That(scroll.TryGetStyleProperty<StyleBoxFlat>(ScrollBar.StylePropertyTrack, out var track), Is.True);
                            Assert.That(track!.BackgroundColor, Is.EqualTo(OrbitraPalettes.PanelBackground));
                        }
                    }
                    var specimenRow = window.SpecimenList.Children.OfType<ContainerButton>().Single();
                    Assert.That(specimenRow, Is.InstanceOf<IOrbitraKeyboardButton>());
                    Assert.That(specimenRow.HasStyleClass(OrbitraButtonStyles.Secondary), Is.True);
                    Assert.That(specimenRow.HasStyleClass("OrbitraEditorControl"), Is.True);
                    Assert.That(specimenRow.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var rowBox), Is.True);
                    Assert.That(rowBox, Is.TypeOf<StyleBoxFlat>());
                    var cards = Descendants(window.BreedRecipes).OfType<PanelContainer>().ToArray();
                    Assert.That(cards, Is.Not.Empty);
                    Assert.That(cards.All(card => card.HasStyleClass("OrbitraWindowSurface") &&
                        card.HasStyleClass("OrbitraEditorControl") && card.PanelOverride == null), Is.True);
                    Assert.That(Descendants(window.BreedDetails).OfType<Button>().All(button =>
                        button is IOrbitraKeyboardButton && button.HasStyleClass("OrbitraEditorControl")), Is.True);
                    Assert.That(window.SpecimenFilter, Is.TypeOf<OrbitraOptionButton>());
                    Assert.That(window.CatalogTierFilter, Is.TypeOf<OrbitraOptionButton>());
                    Assert.That(processor.ProcessButton.HasStyleClass(OrbitraButtonStyles.Primary), Is.True);
                    Assert.That(((StyleBoxFlat) processor.Progress.ForegroundStyleBoxOverride!).BackgroundColor,
                        Is.EqualTo(OrbitraPalettes.Primary.Element));
                });
            }
            finally
            {
                await pair.Client.WaitPost(() => processor?.Dispose());
            }
        });
    }

    private static OrbitraXenobiologySpecimen Specimen(int entity, string identifier) => new()
    {
        Entity = new NetEntity(entity), Identifier = identifier, Name = "Grey slime", Breed = "OrbitraGrey",
        Adult = true, Hunger = 100, Temperature = 293.15f, MinimumTemperature = 283.15f, MaximumTemperature = 313.15f,
        ExtractYield = 1, Offspring = 2, GrowthRate = 1, GrowthBiomass = 10, DivisionBiomass = 20,
    };

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static void AssertInside(Control window, Control control)
    {
        var position = control.GlobalPosition - window.GlobalPosition;
        Assert.That(position.X, Is.GreaterThanOrEqualTo(-1));
        Assert.That(position.Y, Is.GreaterThanOrEqualTo(-1));
        Assert.That(position.X + control.Width, Is.LessThanOrEqualTo(window.Width + 1));
        Assert.That(position.Y + control.Height, Is.LessThanOrEqualTo(window.Height + 1));
    }

    private static void AssertHorizontalInside(Control scroll, Control content, Vector2 viewport)
    {
        var position = content.GlobalPosition - scroll.GlobalPosition;
        var diagnostic = LayoutDiagnostic(content, viewport);
        Assert.That(content.Width, Is.GreaterThan(0), diagnostic);
        Assert.That(position.X, Is.GreaterThanOrEqualTo(-1), diagnostic);
        Assert.That(position.X + content.Width, Is.LessThanOrEqualTo(scroll.Width + 1), diagnostic);
    }

    private static void AssertViewport(Control window, Vector2 viewport)
    {
        Assert.That(window.Size.X, Is.LessThanOrEqualTo(viewport.X));
        Assert.That(window.Size.Y, Is.LessThanOrEqualTo(viewport.Y));
    }

    private static void ArrangeWindow(Control window, Vector2 viewport)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            // Orbitra-Edit: ручная раскладка учитывает очередь дочерних изменений, которую обычно обрабатывает кадр UI.
            foreach (var control in Descendants(window).Reverse().Append(window))
            {
                control.InvalidateMeasure();
                control.InvalidateArrange();
            }
            window.Measure(viewport);
            window.Arrange(UIBox2.FromDimensions(window.Position, window.SetSize));
        }
    }

    private static string LayoutDiagnostic(Control content, Vector2 viewport)
    {
        var path = new List<string> { $"Logical viewport: {viewport}" };
        for (Control? control = content; control != null && path.Count <= 16; control = control.Parent)
            path.Add($"{control.GetType().Name}({control.Name}): Size={control.Size}, Desired={control.DesiredSize}, Set={control.SetSize}, " +
                     $"Visible={control.Visible}/{control.VisibleInTree}, Expand={control.HorizontalExpand}/{control.VerticalExpand}, " +
                     $"Valid={control.IsMeasureValid}/{control.IsArrangeValid}");
        return string.Join("\n", path);
    }

    private static OrbitraXenobiologyProcessorBoundUserInterfaceState ProcessorState(ProcessorContext context)
    {
        Assert.That(context.Pair.Server.System<UserInterfaceSystem>()
            .TryGetUiState<OrbitraXenobiologyProcessorBoundUserInterfaceState>(context.Processor, OrbitraXenobiologyUiKey.Key, out var state), Is.True);
        return state!;
    }

    private static void AssertEmpty(OrbitraXenobiologyProcessorBoundUserInterfaceState state)
    {
        Assert.That(state.Status, Is.EqualTo(OrbitraXenobiologyProcessorStatus.Empty));
        Assert.That(state.HasInput, Is.False);
        Assert.That(state.Input, Is.Null);
        Assert.That(state.Processing, Is.False);
        Assert.That(state.CanProcess, Is.False);
        Assert.That(state.CanEject, Is.False);
    }

    private static void Send(ProcessorContext context, BoundUserInterfaceMessage message)
    {
        message.Actor = context.User;
        context.Pair.Server.System<UserInterfaceSystem>().RaiseUiMessage(context.Processor, OrbitraXenobiologyUiKey.Key, message);
    }

    private static async Task Transfer(ProcessorContext context)
    {
        var server = context.Pair.Server;
        var transferred = false;
        await server.WaitPost(() => transferred = server.System<OrbitraXenobiologyDeviceSystem>().TryTransfer(
            (context.Carrier, server.EntMan.GetComponent<OrbitraXenobiologyCarrierComponent>(context.Carrier)), context.User,
            (context.Processor, server.EntMan.GetComponent<OrbitraXenobiologyProcessorComponent>(context.Processor))));
        await server.WaitAssertion(() => Assert.That(transferred, Is.True));
    }

    private static async Task Click(TestPair pair, BaseButton control)
    {
        var phases = new List<string>();
        var pressed = 0;
        var position = Vector2.Zero;
        var pixelPosition = Vector2.Zero;
        ScreenCoordinates screen = default;
        void OnPressed(BaseButton.ButtonEventArgs _) => pressed++;
        string Describe(string phase)
        {
            var focused = pair.Client.ResolveDependency<IUserInterfaceManager>().ControlFocused;
            return $"{phase}: {control.Name}, Size={control.Size}, Global={control.GlobalPixelPosition}, " +
                   $"Visible={control.VisibleInTree}, Disabled={control.Disabled}, Pressed={control.Pressed}, " +
                   $"Focus={focused?.GetType().Name}({focused?.Name}), OnPressed={pressed}";
        }
        // Orbitra-Edit: кадр UI применяет отложенную раскладку окна и popup до захвата координат реального клика.
        await pair.RunTicksSync(1);
        await pair.Client.WaitAssertion(() =>
        {
            Assert.That(control.Width, Is.GreaterThan(0), Describe("layout before input"));
            Assert.That(control.Height, Is.GreaterThan(0), Describe("layout before input"));
        });
        await pair.Client.WaitPost(() =>
        {
            control.OnPressed += OnPressed;
            position = control.Size / 2;
            pixelPosition = position * control.UIScale;
            screen = new ScreenCoordinates(control.GlobalPixelPosition + pixelPosition, control.Window?.Id ?? default);
            phases.Add(Describe("before down"));
        });
        try
        {
            await pair.Client.DoGuiEvent(control, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick,
                BoundKeyState.Down, screen, default, position, pixelPosition));
            await pair.RunTicksSync(1);
            await pair.Client.WaitPost(() => phases.Add(Describe("after down tick")));
            await pair.Client.DoGuiEvent(control, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick,
                BoundKeyState.Up, screen, default, position, pixelPosition));
            await pair.RunTicksSync(1);
            await pair.Client.WaitPost(() => phases.Add(Describe("after up tick")));
            await pair.Client.WaitAssertion(() => Assert.That(pressed, Is.EqualTo(1), string.Join("\n", phases)));
        }
        finally
        {
            await pair.Client.WaitPost(() => control.OnPressed -= OnPressed);
        }
    }

    private static async Task WithConsoleWindow(Func<TestPair, OrbitraXenobiologyWindow, Task> scenario)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.RunTicksSync(15);
        OrbitraXenobiologyWindow window = null!;
        try
        {
            await pair.Client.WaitPost(() =>
            {
                window = new OrbitraXenobiologyWindow();
                window.OpenCentered();
            });
            await scenario(pair, window);
        }
        finally
        {
            await pair.Client.WaitPost(() => window?.Parent?.RemoveChild(window));
        }
        await pair.CleanReturnAsync();
    }

    private static async Task WithProcessor(Func<ProcessorContext, Task> scenario, bool connected = false)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = connected });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var ui = server.System<UserInterfaceSystem>();
        var previous = pair.Player?.AttachedEntity;
        EntityUid user = default, carrier = default, processor = default, corpse = default;
        var inserted = false;
        var equipped = false;
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
                var coordinates = new EntityCoordinates(map.Grid, 1.5f, 1.5f);
                user = server.EntMan.SpawnEntity("MobHuman", coordinates);
                server.System<SharedGodmodeSystem>().EnableGodmode(user);
                carrier = server.EntMan.SpawnEntity("OrbitraXenoCarrier", coordinates);
                processor = server.EntMan.SpawnEntity("OrbitraXenoProcessor", new EntityCoordinates(map.Grid, 1.5f, 2.5f));
                corpse = server.EntMan.SpawnEntity("OrbitraXenoSlimeGreyAdult", new EntityCoordinates(map.Grid, 2.25f, 1.5f));
                server.System<MobStateSystem>().ChangeMobState(corpse, MobState.Dead);
                var contents = server.System<SharedContainerSystem>().EnsureContainer<Robust.Shared.Containers.Container>(carrier, "xeno_carrier");
                inserted = server.System<SharedContainerSystem>().Insert(corpse, contents);
                equipped = server.System<SharedHandsSystem>().TryPickupAnyHand(user, carrier);
                server.System<PowerReceiverSystem>().SetNeedsPower(processor, false);
                if (connected)
                    server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            });
            await pair.RunTicksSync(3);
            await server.WaitPost(() => opened = ui.TryOpenUi(processor, OrbitraXenobiologyUiKey.Key, user));
            await server.WaitAssertion(() =>
            {
                Assert.That(inserted, Is.True);
                Assert.That(equipped, Is.True);
                Assert.That(opened, Is.True);
            });
            if (connected)
                await pair.RunTicksSync(15);
            await scenario(new ProcessorContext(pair, user, carrier, processor, corpse));
            completed = true;
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (server.EntMan.EntityExists(processor))
                    ui.CloseUi(processor, OrbitraXenobiologyUiKey.Key);
                if (connected)
                    server.PlayerMan.SetAttachedEntity(pair.Player!, previous);
                server.EntMan.DeleteEntity(map.MapUid);
            });
            await pair.RunTicksSync(5);
            if (completed)
                await pair.CleanReturnAsync();
        }
    }

    private sealed record ProcessorContext(TestPair Pair, EntityUid User, EntityUid Carrier, EntityUid Processor, EntityUid Corpse);
}
