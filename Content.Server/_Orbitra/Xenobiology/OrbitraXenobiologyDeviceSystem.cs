// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Server.Power.Components;
using Content.Shared._Orbitra.Xenobiology;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mind.Components;
using Content.Shared.NameIdentifier;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Temperature.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Xenobiology;

/// <summary>Owns safe specimen transport, powered extraction and read-only colony measurements.</summary>
public sealed partial class OrbitraXenobiologyDeviceSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private OrbitraXenobiologySystem _xenobiology = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityQuery<OrbitraXenobiologySlimeComponent> _slimeQuery = default!;
    [Dependency] private EntityQuery<TemperatureComponent> _temperatureQuery = default!;
    [Dependency] private EntityQuery<NameIdentifierComponent> _nameIdentifierQuery = default!;
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraXenobiologyCarrierComponent, ComponentInit>(OnCarrierInit);
        SubscribeLocalEvent<OrbitraXenobiologyCarrierComponent, AfterInteractEvent>(OnCarrierInteract);
        SubscribeLocalEvent<OrbitraXenobiologyCarrierComponent, UseInHandEvent>(OnCarrierUse);
        SubscribeLocalEvent<OrbitraXenobiologyCarrierComponent, OrbitraXenobiologyCaptureDoAfterEvent>(OnCaptured);
        SubscribeLocalEvent<OrbitraXenobiologyCarrierComponent, ContainerIsInsertingAttemptEvent>(OnCarrierInsert);
        SubscribeLocalEvent<OrbitraXenobiologyProcessorComponent, ComponentInit>(OnProcessorInit);
        SubscribeLocalEvent<OrbitraXenobiologyProcessorComponent, ContainerIsInsertingAttemptEvent>(OnProcessorInsert);
        SubscribeLocalEvent<OrbitraXenobiologyProcessorComponent, EntInsertedIntoContainerMessage>(OnProcessorInserted);
        SubscribeLocalEvent<OrbitraXenobiologyProcessorComponent, EntRemovedFromContainerMessage>(OnProcessorRemoved);
        SubscribeLocalEvent<OrbitraXenobiologyProcessorComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<OrbitraXenobiologyConsoleComponent, PowerChangedEvent>(OnConsolePowerChanged);
        Subs.BuiEvents<OrbitraXenobiologyProcessorComponent>(OrbitraXenobiologyUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnProcessorOpened);
            subs.Event<OrbitraXenobiologyProcessMessage>(OnProcess);
            subs.Event<OrbitraXenobiologyEjectMessage>(OnEject);
        });
        Subs.BuiEvents<OrbitraXenobiologyConsoleComponent>(OrbitraXenobiologyUiKey.Key,
            subs => subs.Event<BoundUIOpenedEvent>(OnConsoleOpened));
    }

    private void OnCarrierInit(Entity<OrbitraXenobiologyCarrierComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.Container);
    }

    private void OnProcessorInit(Entity<OrbitraXenobiologyProcessorComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.Container);
    }

    private void OnCarrierInsert(Entity<OrbitraXenobiologyCarrierComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == ent.Comp.Container &&
            (!_slimeQuery.TryGetComponent(args.EntityUid, out var slime) || slime.Consumed ||
             !args.AssumeEmpty && args.Container.ContainedEntities.Count >= ent.Comp.Capacity))
            args.Cancel();
    }

    private void OnProcessorInsert(Entity<OrbitraXenobiologyProcessorComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == ent.Comp.Container && !IsHarvestable(args.EntityUid))
            args.Cancel();
    }

    private void OnProcessorInserted(Entity<OrbitraXenobiologyProcessorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.Container)
            UpdateProcessor(ent);
    }

    private void OnProcessorRemoved(Entity<OrbitraXenobiologyProcessorComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Container)
            return;

        RemCompDeferred<OrbitraXenobiologyProcessingComponent>(ent);
        UpdateProcessor(ent);
    }

    private void OnCarrierInteract(Entity<OrbitraXenobiologyCarrierComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryComp<OrbitraXenobiologyProcessorComponent>(target, out var processor))
            args.Handled = TryTransfer(ent, args.User, (target, processor));
        else if (_slimeQuery.HasComp(target))
            args.Handled = TryCapture(ent, args.User, target);
    }

    /// <summary>Starts a cancellable capture with a held carrier and a reachable, uncontained slime.</summary>
    public bool TryCapture(Entity<OrbitraXenobiologyCarrierComponent> carrier, EntityUid user, EntityUid target)
    {
        if (!CanCapture(carrier, user, target))
            return false;

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, carrier.Comp.CaptureDelay,
            new OrbitraXenobiologyCaptureDoAfterEvent(), carrier, target: target, used: carrier)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 2f,
        });
    }

    /// <summary>Checks all capture conditions without changing specimen or container state.</summary>
    public bool CanCapture(Entity<OrbitraXenobiologyCarrierComponent> carrier, EntityUid user, EntityUid target)
    {
        return !TerminatingOrDeleted(carrier) && !TerminatingOrDeleted(target) &&
               _hands.IsHolding(user, carrier) && _interaction.InRangeUnobstructed(user, target) &&
               _actionBlocker.CanInteract(user, target) &&
               !(TryComp<MindContainerComponent>(target, out var mind) && mind.HasMind) &&
               _slimeQuery.TryGetComponent(target, out var slime) && !slime.Consumed &&
               !_container.IsEntityInContainer(target) &&
               _container.TryGetContainer(carrier, carrier.Comp.Container, out var contents) &&
               contents.ContainedEntities.Count < carrier.Comp.Capacity;
    }

    private void OnCaptured(Entity<OrbitraXenobiologyCarrierComponent> ent, ref OrbitraXenobiologyCaptureDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !CanCapture(ent, args.User, target))
            return;

        if (_container.TryGetContainer(ent, ent.Comp.Container, out var contents))
        {
            args.Handled = _container.Insert(target, contents);
            if (args.Handled)
                _popup.PopupEntity(Loc.GetString("orbitra-xeno-captured"), ent, args.User);
        }
    }

    private void OnCarrierUse(Entity<OrbitraXenobiologyCarrierComponent> ent, ref UseInHandEvent args)
    {
        if (!args.Handled)
            args.Handled = TryRelease(ent, args.User);
    }

    /// <summary>Releases one specimen at the holder's position through the normal container API.</summary>
    public bool TryRelease(Entity<OrbitraXenobiologyCarrierComponent> carrier, EntityUid user)
    {
        if (!_hands.IsHolding(user, carrier) || !_actionBlocker.CanInteract(user, null) ||
            !_container.TryGetContainer(carrier, carrier.Comp.Container, out var contents) ||
            contents.ContainedEntities.Count == 0)
            return false;

        return _container.Remove(contents.ContainedEntities[0], contents,
            destination: Transform(user).Coordinates);
    }

    /// <summary>Transfers a corpse from the held carrier into the reached processor without dropping it.</summary>
    public bool TryTransfer(Entity<OrbitraXenobiologyCarrierComponent> carrier, EntityUid user,
        Entity<OrbitraXenobiologyProcessorComponent> processor)
    {
        if (!CanUse(processor, user) || !_hands.IsHolding(user, carrier) ||
            IsProcessing(processor) ||
            !_container.TryGetContainer(carrier, carrier.Comp.Container, out var source) ||
            source.ContainedEntities.Count == 0 ||
            !_container.TryGetContainer(processor, processor.Comp.Container, out var destination))
            return false;

        var corpse = source.ContainedEntities[0];
        return IsHarvestable(corpse) && _container.Insert(corpse, destination);
    }

    private bool IsHarvestable(EntityUid slime)
    {
        return _xenobiology.CanHarvest(slime);
    }

    private bool IsPowered(EntityUid machine)
    {
        return TryComp<ApcPowerReceiverComponent>(machine, out var receiver) && receiver.Powered;
    }

    private bool CanUse(EntityUid machine, EntityUid user)
    {
        return !TerminatingOrDeleted(machine) && !TerminatingOrDeleted(user) &&
               _actionBlocker.CanInteract(user, machine) &&
               _interaction.InRangeUnobstructed(user, machine);
    }

    private EntityUid? GetInput(Entity<OrbitraXenobiologyProcessorComponent> processor)
    {
        if (_container.TryGetContainer(processor, processor.Comp.Container, out var contents) &&
            contents.ContainedEntities.Count > 0)
            return contents.ContainedEntities[0];

        return null;
    }

    /// <summary>Checks that a powered, idle processor contains an adult corpse which has not been harvested.</summary>
    public bool CanProcess(Entity<OrbitraXenobiologyProcessorComponent> processor)
    {
        return IsPowered(processor) && !IsProcessing(processor) &&
               GetInput(processor) is { } input && IsHarvestable(input);
    }

    /// <summary>Starts powered processing; source eligibility is checked again on completion.</summary>
    public bool TryProcess(Entity<OrbitraXenobiologyProcessorComponent> processor, EntityUid user)
    {
        if (!CanUse(processor, user) || !CanProcess(processor))
            return false;

        var processing = EnsureComp<OrbitraXenobiologyProcessingComponent>(processor);
        processing.StartedAt = _timing.CurTime;
        processing.FinishAt = _timing.CurTime + TimeSpan.FromSeconds(Math.Max(1f, processor.Comp.Duration));
        UpdateProcessor(processor);
        return true;
    }

    private void OnProcess(Entity<OrbitraXenobiologyProcessorComponent> ent, ref OrbitraXenobiologyProcessMessage args)
    {
        if (!TryProcess(ent, args.Actor))
            UpdateProcessor(ent);
    }

    private void OnEject(Entity<OrbitraXenobiologyProcessorComponent> ent, ref OrbitraXenobiologyEjectMessage args)
    {
        if (!TryEject(ent, args.Actor))
            UpdateProcessor(ent);
    }

    /// <summary>Cancels processing and returns the corpse without spending its extraction yield.</summary>
    public bool TryEject(Entity<OrbitraXenobiologyProcessorComponent> processor, EntityUid user)
    {
        if (!CanUse(processor, user) || GetInput(processor) is not { } input ||
            !_container.TryGetContainer(processor, processor.Comp.Container, out var contents))
            return false;

        RemComp<OrbitraXenobiologyProcessingComponent>(processor);
        var removed = _container.Remove(input, contents, destination: Transform(user).Coordinates);
        UpdateProcessor(processor);
        return removed;
    }

    private void OnPowerChanged(Entity<OrbitraXenobiologyProcessorComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            RemCompDeferred<OrbitraXenobiologyProcessingComponent>(ent);
        UpdateProcessor(ent);
    }

    private void OnConsolePowerChanged(Entity<OrbitraXenobiologyConsoleComponent> ent, ref PowerChangedEvent args)
    {
        UpdateConsole(ent);
    }

    private void OnProcessorOpened(Entity<OrbitraXenobiologyProcessorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateProcessor(ent);
    }

    private void OnConsoleOpened(Entity<OrbitraXenobiologyConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateConsole(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);

        var processing = EntityQueryEnumerator<OrbitraXenobiologyProcessingComponent, OrbitraXenobiologyProcessorComponent>();
        while (processing.MoveNext(out var uid, out var active, out var processor))
        {
            if (!active.Running)
                continue;

            if (!IsPowered(uid) || GetInput((uid, processor)) is not { } input || !IsHarvestable(input))
            {
                RemCompDeferred<OrbitraXenobiologyProcessingComponent>(uid);
                UpdateProcessor((uid, processor));
                continue;
            }

            if (_timing.CurTime < active.FinishAt)
                continue;

            RemCompDeferred<OrbitraXenobiologyProcessingComponent>(uid);
            if (!_container.TryGetContainer(uid, processor.Container, out var contents) ||
                !_container.Remove(input, contents, destination: Transform(uid).Coordinates))
            {
                UpdateProcessor((uid, processor));
                continue;
            }

            if (!_xenobiology.TryHarvest(input, out var breed, out var yield))
            {
                UpdateProcessor((uid, processor));
                continue;
            }

            var extract = _prototype.Index(breed).Extract;
            for (var i = 0; i < yield; i++)
                Spawn(extract, Transform(uid).Coordinates);
            QueueDel(input);
            UpdateProcessor((uid, processor));
        }

        var processors = EntityQueryEnumerator<OrbitraXenobiologyProcessorComponent>();
        while (processors.MoveNext(out var uid, out var processor))
        {
            if (_ui.IsUiOpen(uid, OrbitraXenobiologyUiKey.Key))
                UpdateProcessor((uid, processor));
        }

        var consoles = EntityQueryEnumerator<OrbitraXenobiologyConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (_ui.IsUiOpen(uid, OrbitraXenobiologyUiKey.Key))
                UpdateConsole((uid, console));
        }
    }

    private bool IsProcessing(EntityUid processor)
    {
        return TryComp<OrbitraXenobiologyProcessingComponent>(processor, out var processing) && processing.Running;
    }

    private void UpdateProcessor(Entity<OrbitraXenobiologyProcessorComponent> ent)
    {
        var input = GetInput(ent);
        var harvestable = input is { } specimen && IsHarvestable(specimen);
        var state = new OrbitraXenobiologyProcessorBoundUserInterfaceState
        {
            Powered = IsPowered(ent),
            CanProcess = CanProcess(ent),
            HasInput = input != null,
            CanEject = input != null,
            Duration = Math.Max(1f, ent.Comp.Duration),
        };
        if (input is { } uid && _slimeQuery.TryGetComponent(uid, out var slime))
        {
            state.Input = Measure(uid, slime);
            state.ExpectedYield = harvestable ? Math.Clamp(slime.ExtractYield, 1, 3) : 0;
        }

        if (state.Powered && harvestable &&
            TryComp<OrbitraXenobiologyProcessingComponent>(ent, out var processing) && processing.Running)
        {
            state.Processing = true;
            state.Duration = Math.Max(1f, (float) (processing.FinishAt - processing.StartedAt).TotalSeconds);
            state.SecondsRemaining = Math.Clamp((float) (processing.FinishAt - _timing.CurTime).TotalSeconds,
                0f, state.Duration);
            state.Progress = Math.Clamp(1f - state.SecondsRemaining / state.Duration, 0f, 1f);
        }

        state.Status = !state.Powered ? OrbitraXenobiologyProcessorStatus.NoPower
            : !state.HasInput ? OrbitraXenobiologyProcessorStatus.Empty
            : !harvestable ? OrbitraXenobiologyProcessorStatus.InvalidInput
            : state.Processing ? OrbitraXenobiologyProcessorStatus.Processing
            : OrbitraXenobiologyProcessorStatus.Ready;
        _ui.SetUiState(ent.Owner, OrbitraXenobiologyUiKey.Key, state);
    }

    private void UpdateConsole(Entity<OrbitraXenobiologyConsoleComponent> ent)
    {
        var state = new OrbitraXenobiologyBoundUserInterfaceState { Powered = IsPowered(ent), Range = ent.Comp.Range };
        if (state.Powered)
        {
            var origin = Transform(ent);
            var position = _transform.GetWorldPosition(origin);
            var slimes = EntityQueryEnumerator<OrbitraXenobiologySlimeComponent, TransformComponent>();
            while (slimes.MoveNext(out var uid, out var slime, out var transform))
            {
                if (!slime.Consumed && transform.GridUid == origin.GridUid && transform.MapUid == origin.MapUid &&
                    (_transform.GetWorldPosition(transform) - position).LengthSquared() <= ent.Comp.Range * ent.Comp.Range)
                    state.Specimens.Add(Measure(uid, slime));
            }
        }
        state.Specimens.Sort(static (first, second) => first.Entity.Id.CompareTo(second.Entity.Id));
        _ui.SetUiState(ent.Owner, OrbitraXenobiologyUiKey.Key, state);
    }

    private OrbitraXenobiologySpecimen Measure(EntityUid uid, OrbitraXenobiologySlimeComponent slime)
    {
        var breed = _prototype.Index(slime.Breed);
        var metadata = MetaData(uid);
        return new OrbitraXenobiologySpecimen
        {
            Entity = GetNetEntity(uid, metadata),
            Name = metadata.EntityName,
            Identifier = _nameIdentifierQuery.TryGetComponent(uid, out var identifier) ? identifier.FullIdentifier : string.Empty,
            Breed = breed.ID,
            Adult = slime.Adult,
            Dead = _mobState.IsDead(uid),
            Hostile = slime.Hostile,
            Suspended = slime.Suspended || _container.IsEntityInContainer(uid),
            Generation = slime.Generation,
            Growth = Math.Clamp(slime.Growth / Math.Max(1f, breed.GrowthDuration), 0f, 1f),
            Biomass = slime.Biomass,
            Hunger = TryComp<SatiationComponent>(uid, out var satiation)
                ? _satiation.GetValueOrNull((uid, satiation), SatiationSystem.Hunger) ?? 0f : 0f,
            Temperature = _temperatureQuery.TryGetComponent(uid, out var temperature) ? temperature.Temperature : 0f,
            MinimumTemperature = breed.MinimumTemperature - slime.ThermalTolerance,
            MaximumTemperature = breed.MaximumTemperature + slime.ThermalTolerance,
            GrowthBiomass = breed.GrowthBiomass,
            DivisionBiomass = breed.DivisionBiomass,
            MutationChance = slime.MutationChance,
            Offspring = slime.Offspring,
            ExtractYield = slime.ExtractYield,
            GrowthRate = slime.GrowthRate,
            Docility = slime.Docility,
            ThermalTolerance = slime.ThermalTolerance,
        };
    }
}
