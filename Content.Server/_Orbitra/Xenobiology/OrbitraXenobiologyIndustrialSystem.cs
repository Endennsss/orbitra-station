// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Diagnostics.CodeAnalysis;
using Content.Server.NPC.HTN;
using Content.Server.Stack;
using Content.Shared._Orbitra.Xenobiology;
using Content.Shared.ActionBlocker;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Xenobiology;

/// <summary>Consumes induction inputs once and runs bounded, interruptible industrial production cycles.</summary>
public sealed partial class OrbitraXenobiologyIndustrialSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private EntityQuery<OrbitraXenobiologyIndustrialComponent> _industrialQuery = default!;
    [Dependency] private EntityQuery<OrbitraXenobiologyIndustrialActiveComponent> _activeQuery = default!;

    private const int MaximumBatchItems = 8;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraXenobiologyIndustrialCatalystComponent, AfterInteractEvent>(OnCatalystInteract);
        SubscribeLocalEvent<OrbitraXenobiologyExtractComponent, AfterInteractEvent>(OnExtractInteract);
        SubscribeLocalEvent<OrbitraXenobiologyIndustrialInductionComponent, ExaminedEvent>(OnInductionExamined);
        SubscribeLocalEvent<OrbitraXenobiologyIndustrialComponent, ExaminedEvent>(OnIndustrialExamined);
        SubscribeLocalEvent<OrbitraXenobiologyIndustrialComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<OrbitraXenobiologyIndustrialComponent, SolutionChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<OrbitraXenobiologyIndustrialComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<OrbitraXenobiologyIndustrialComponent, EntGotRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<OrbitraXenobiologyIndustrialComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<OrbitraXenobiologyIndustrialComponent, EntityPausedEvent>(OnPaused);
        SubscribeLocalEvent<OrbitraXenobiologyIndustrialComponent, EntityUnpausedEvent>(OnUnpaused);
    }

    private void OnCatalystInteract(Entity<OrbitraXenobiologyIndustrialCatalystComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Handled && args.Target is { } target)
            args.Handled = TryInduce(ent, args.User, target);
    }

    private void OnExtractInteract(Entity<OrbitraXenobiologyExtractComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Handled && args.Target is { } target && HasComp<OrbitraXenobiologyIndustrialInductionComponent>(target))
            args.Handled = TryFeed(ent, args.User, target);
    }

    private void OnInductionExamined(Entity<OrbitraXenobiologyIndustrialInductionComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange && _prototype.TryIndex(ent.Comp.Recipe, out var recipe))
            args.PushMarkup(Loc.GetString("orbitra-xeno-industrial-progress",
                ("current", ent.Comp.ExtractsFed), ("required", recipe.RequiredExtracts),
                ("breed", Loc.GetString(_prototype.Index(recipe.Breed).Name))));
    }

    private void OnIndustrialExamined(Entity<OrbitraXenobiologyIndustrialComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !_prototype.TryIndex(ent.Comp.Recipe, out var recipe))
            return;

        args.PushMarkup(Loc.GetString("orbitra-xeno-industrial-cycle", ("plasma", recipe.PlasmaCost),
            ("seconds", recipe.Interval.TotalSeconds)));
        foreach (var (product, count) in recipe.Products)
            args.PushMarkup(Loc.GetString("orbitra-xeno-industrial-product", ("count", count),
                ("name", _prototype.Index(product).Name)));
        args.PushMarkup(Loc.GetString(CanStartProduction(ent)
            ? "orbitra-xeno-industrial-running"
            : "orbitra-xeno-industrial-stopped"));
    }

    private void OnStartup(Entity<OrbitraXenobiologyIndustrialComponent> ent, ref ComponentStartup args)
    {
        RefreshProduction(ent);
    }

    private void OnSolutionChanged(Entity<OrbitraXenobiologyIndustrialComponent> ent, ref SolutionChangedEvent args)
    {
        if (!ent.Comp.Producing && args.Solution.Comp.Id == ent.Comp.Solution)
            RefreshProduction(ent);
    }

    private void OnInserted(Entity<OrbitraXenobiologyIndustrialComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        ResetCycle(ent);
    }

    private void OnRemoved(Entity<OrbitraXenobiologyIndustrialComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        ResetCycle(ent);
        RefreshProduction(ent);
    }

    private void OnParentChanged(Entity<OrbitraXenobiologyIndustrialComponent> ent, ref EntParentChangedMessage args)
    {
        ResetCycle(ent);
    }

    private void OnPaused(Entity<OrbitraXenobiologyIndustrialComponent> ent, ref EntityPausedEvent args)
    {
        ResetCycle(ent);
    }

    private void OnUnpaused(Entity<OrbitraXenobiologyIndustrialComponent> ent, ref EntityUnpausedEvent args)
    {
        ResetCycle(ent);
    }

    /// <summary>Claims one held research catalyst before attaching induction progress to the selected adult.</summary>
    public bool TryInduce(EntityUid catalyst, EntityUid user, EntityUid slime)
    {
        if (!CanInduce(catalyst, user, slime, false) ||
            !TryComp<OrbitraXenobiologyIndustrialCatalystComponent>(catalyst, out var source) ||
            !TryComp<OrbitraXenobiologySlimeComponent>(slime, out var state) ||
            !TryGetRecipe(state.Breed, out var recipe))
            return false;

        source.Used = true;
        var induction = AddComp<OrbitraXenobiologyIndustrialInductionComponent>(slime);
        induction.Recipe = recipe.ID;
        QueueDel(catalyst);
        _popup.PopupEntity(Loc.GetString("orbitra-xeno-industrial-induced", ("required", recipe.RequiredExtracts)), slime, user);
        return true;
    }

    /// <summary>Checks held access, living NPC adulthood and support without consuming the research gate.</summary>
    public bool CanInduce(EntityUid catalyst, EntityUid user, EntityUid slime, bool quiet = true)
    {
        if (!CanUseHeld(catalyst, user, slime) ||
            !TryComp<OrbitraXenobiologyIndustrialCatalystComponent>(catalyst, out var source) || source.Used)
            return Fail(user, slime, "orbitra-xeno-industrial-access", quiet);
        if (!IsEligibleSlime(slime, out var state))
            return Fail(user, slime, "orbitra-xeno-industrial-specimen", quiet);
        if (HasComp<OrbitraXenobiologyIndustrialInductionComponent>(slime))
            return Fail(user, slime, "orbitra-xeno-industrial-already-induced", quiet);
        return TryGetRecipe(state.Breed, out _) || Fail(user, slime, "orbitra-xeno-industrial-unsupported", quiet);
    }

    /// <summary>Claims an unused same-breed extract before completing one irreversible transformation.</summary>
    public bool TryFeed(EntityUid extract, EntityUid user, EntityUid slime)
    {
        if (!CanFeed(extract, user, slime, false) ||
            !TryComp<OrbitraXenobiologyExtractComponent>(extract, out var source) ||
            !TryComp<OrbitraXenobiologyIndustrialInductionComponent>(slime, out var induction) ||
            !_prototype.TryIndex(induction.Recipe, out var recipe))
            return false;

        // Orbitra-Edit: оба входа заявляются израсходованными до спавна и событий удаления.
        source.Used = true;
        induction.ExtractsFed++;
        if (induction.ExtractsFed >= recipe.RequiredExtracts)
        {
            induction.Consumed = true;
            Comp<OrbitraXenobiologySlimeComponent>(slime).Consumed = true;
            Spawn(recipe.Extract, Transform(slime).Coordinates);
            QueueDel(slime);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("orbitra-xeno-industrial-progress", ("current", induction.ExtractsFed),
                ("required", recipe.RequiredExtracts), ("breed", Loc.GetString(_prototype.Index(recipe.Breed).Name))), slime, user);
        }

        QueueDel(extract);
        return true;
    }

    /// <summary>Checks the immutable induction recipe, source lineage and all actor safety conditions.</summary>
    public bool CanFeed(EntityUid extract, EntityUid user, EntityUid slime, bool quiet = true)
    {
        if (!CanUseHeld(extract, user, slime))
            return Fail(user, slime, "orbitra-xeno-industrial-access", quiet);
        if (!IsEligibleSlime(slime, out var state) ||
            !TryComp<OrbitraXenobiologyIndustrialInductionComponent>(slime, out var induction) || induction.Consumed ||
            !_prototype.TryIndex(induction.Recipe, out var recipe) || !IsValidRecipe(recipe) || state.Breed != recipe.Breed ||
            induction.ExtractsFed < 0 || induction.ExtractsFed >= recipe.RequiredExtracts)
            return Fail(user, slime, "orbitra-xeno-industrial-specimen", quiet);
        if (!TryComp<OrbitraXenobiologyExtractComponent>(extract, out var source) || source.Used)
            return Fail(user, slime, "orbitra-xeno-industrial-unused", quiet);
        return source.Breed == recipe.Breed || Fail(user, slime, "orbitra-xeno-industrial-breed", quiet);
    }

    /// <summary>Starts a fresh full cycle; existing uninterrupted cycles keep their deadline.</summary>
    public bool TryStartProduction(EntityUid extract)
    {
        if (!CanStartProduction(extract) || !_industrialQuery.TryComp(extract, out var industrial))
            return false;

        var active = EnsureComp<OrbitraXenobiologyIndustrialActiveComponent>(extract);
        active.NextProduction ??= _timing.CurTime + _prototype.Index(industrial.Recipe).Interval;
        return true;
    }

    /// <summary>Requires real non-space floor, no ancestor container, an unpaused item and sufficient plasma.</summary>
    public bool CanStartProduction(EntityUid extract)
    {
        return !TerminatingOrDeleted(extract) && !IsPaused(extract) &&
               _industrialQuery.TryComp(extract, out var industrial) && !industrial.Producing &&
               TryGetFuel(extract, industrial, out _, out _) && TryGetFloor(extract, out _);
    }

    /// <summary>Consumes fuel and the due cycle before any chemistry callbacks or finite output spawns.</summary>
    public bool TryProduce(EntityUid extract)
    {
        if (!CanProduce(extract) || !_industrialQuery.TryComp(extract, out var industrial) ||
            !TryGetFuel(extract, industrial, out var recipe, out var solution) || !TryGetFloor(extract, out var coordinates))
            return false;

        industrial.Producing = true;
        ResetCycle(extract);
        try
        {
            if (_solutionContainer.RemoveReagent(solution, "Plasma", recipe.PlasmaCost) != recipe.PlasmaCost ||
                TerminatingOrDeleted(extract) || IsPaused(extract) || !TryGetFloor(extract, out coordinates))
                return false;

            foreach (var (product, count) in recipe.Products)
            {
                var remaining = count;
                while (remaining > 0)
                {
                    if (TerminatingOrDeleted(extract) || IsPaused(extract) || !TryGetFloor(extract, out coordinates))
                        return false;

                    var maxCount = _stack.GetMaxCount(product);
                    if (maxCount <= 0)
                        return false;

                    // Orbitra-Edit: Products описывает единицы материала, а не отдельные одноштучные предметы.
                    var units = Math.Min(remaining, maxCount);
                    remaining -= units;
                    _stack.SpawnMultipleAtPosition(product, units, coordinates);
                }
            }
        }
        finally
        {
            industrial.Producing = false;
            if (!TerminatingOrDeleted(extract))
                RefreshProduction((extract, industrial));
        }
        return true;
    }

    /// <summary>Checks the current cycle deadline as well as fuel and placement without changing progress.</summary>
    public bool CanProduce(EntityUid extract)
    {
        return CanStartProduction(extract) && _activeQuery.TryComp(extract, out var active) &&
               active.NextProduction is { } deadline && deadline <= _timing.CurTime;
    }

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<OrbitraXenobiologyIndustrialActiveComponent, OrbitraXenobiologyIndustrialComponent>();
        while (query.MoveNext(out var uid, out var active, out _))
        {
            if (!CanStartProduction(uid))
            {
                active.NextProduction = null;
                continue;
            }
            if (active.NextProduction == null)
                TryStartProduction(uid);
            else if (active.NextProduction <= _timing.CurTime)
                TryProduce(uid);
        }
    }

    private bool CanUseHeld(EntityUid source, EntityUid user, EntityUid target)
    {
        return !TerminatingOrDeleted(source) && !TerminatingOrDeleted(user) && !TerminatingOrDeleted(target) &&
               !IsPaused(source) && !IsPaused(user) && !IsPaused(target) && _hands.IsHolding(user, source) &&
               _actionBlocker.CanInteract(user, target) && _interaction.InRangeUnobstructed(user, target);
    }

    private bool IsEligibleSlime(EntityUid slime, [NotNullWhen(true)] out OrbitraXenobiologySlimeComponent? state)
    {
        return TryComp(slime, out state) && state.Adult && !state.Consumed && !state.Suspended &&
               _mobState.IsAlive(slime) && HasComp<HTNComponent>(slime) && !HasComp<ActorComponent>(slime) &&
               (!TryComp<MindContainerComponent>(slime, out var mind) || !mind.HasMind) &&
               !_container.IsEntityOrParentInContainer(slime);
    }

    private bool TryGetRecipe(ProtoId<OrbitraXenobiologyBreedPrototype> breed,
        [NotNullWhen(true)] out OrbitraXenobiologyIndustrialRecipePrototype? recipe)
    {
        recipe = null;
        foreach (var candidate in _prototype.EnumeratePrototypes<OrbitraXenobiologyIndustrialRecipePrototype>())
        {
            if (candidate.Breed == breed && IsValidRecipe(candidate) &&
                (recipe == null || string.CompareOrdinal(candidate.ID, recipe.ID) < 0))
                recipe = candidate;
        }
        return recipe != null;
    }

    private bool IsValidRecipe(OrbitraXenobiologyIndustrialRecipePrototype recipe)
    {
        if (recipe.RequiredExtracts is < 1 or > 100 || recipe.PlasmaCost <= FixedPoint2.Zero ||
            recipe.Interval < TimeSpan.FromSeconds(1) || recipe.Interval > TimeSpan.FromDays(1) || recipe.Products.Count == 0)
            return false;

        var total = 0;
        foreach (var (product, count) in recipe.Products)
        {
            if (count is < 1 or > MaximumBatchItems || (total += count) > MaximumBatchItems ||
                !_prototype.TryIndex(product, out var prototype) || prototype.Abstract || _stack.GetMaxCount(prototype) <= 0)
                return false;
        }
        return true;
    }

    private bool TryGetFuel(EntityUid extract, OrbitraXenobiologyIndustrialComponent industrial,
        [NotNullWhen(true)] out OrbitraXenobiologyIndustrialRecipePrototype? recipe, out Entity<SolutionComponent> solution)
    {
        solution = default;
        if (!_prototype.TryIndex(industrial.Recipe, out recipe) || !IsValidRecipe(recipe) ||
            !_solutionContainer.TryGetSolution(extract, industrial.Solution, out var solutionEntity, out var mixture) ||
            mixture.GetTotalPrototypeQuantity("Plasma") < recipe.PlasmaCost)
            return false;

        solution = solutionEntity.Value;
        return true;
    }

    private bool TryGetFloor(EntityUid extract, out EntityCoordinates coordinates)
    {
        coordinates = default;
        if (_container.IsEntityOrParentInContainer(extract) ||
            !_transform.TryGetMapOrGridCoordinates(extract, out var output) ||
            _turf.GetTileRef(output.Value) is not { } tile || tile.Tile.IsEmpty || _turf.IsSpace(tile))
            return false;

        coordinates = output.Value;
        return true;
    }

    private void RefreshProduction(Entity<OrbitraXenobiologyIndustrialComponent> ent)
    {
        if (TerminatingOrDeleted(ent) || ent.Comp.Producing)
            return;

        if (!TryGetFuel(ent, ent.Comp, out _, out _))
        {
            RemComp<OrbitraXenobiologyIndustrialActiveComponent>(ent);
            return;
        }

        // Orbitra-Edit: заправленный предмет отслеживается и в контейнере, чтобы после извлечения начать новый цикл.
        EnsureComp<OrbitraXenobiologyIndustrialActiveComponent>(ent);
        if (!TryStartProduction(ent))
            ResetCycle(ent);
    }

    private void ResetCycle(EntityUid extract)
    {
        if (_activeQuery.TryComp(extract, out var active))
            active.NextProduction = null;
    }

    private bool Fail(EntityUid user, EntityUid target, LocId message, bool quiet)
    {
        if (!quiet && !TerminatingOrDeleted(user) && !TerminatingOrDeleted(target))
            _popup.PopupEntity(Loc.GetString(message), target, user);
        return false;
    }
}
