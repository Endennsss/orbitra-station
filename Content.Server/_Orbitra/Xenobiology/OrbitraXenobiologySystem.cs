// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Server.NPC.HTN;
using Content.Shared._Orbitra.Xenobiology;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Temperature.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Orbitra.Xenobiology;

/// <summary>Runs feeding, maturation, inherited traits and controlled division of laboratory slimes.</summary>
public sealed partial class OrbitraXenobiologySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private SatiationSystem _satiation = default!;

    [Dependency] private EntityQuery<OrbitraXenobiologySlimeComponent> _slimeQuery = default!;
    [Dependency] private EntityQuery<HTNComponent> _htnQuery = default!;
    [Dependency] private EntityQuery<SatiationComponent> _satiationQuery = default!;
    [Dependency] private EntityQuery<TemperatureComponent> _temperatureQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _transformQuery = default!;

    /// <summary>Map-wide colony limit shared by division and culture hatching; unprocessed corpses also count.</summary>
    public const int PopulationLimit = 64;

    private const float UpdateInterval = 1f;
    private const float MinimumGrowthHunger = 25f;
    private const float DivisionHunger = 50f;
    private const string CalmFaction = "OrbitraXenoSlimeCalm";
    private const string HungryFaction = "OrbitraXenoSlimeHungry";
    private static readonly ProtoId<OrbitraXenobiologyBreedPrototype> Rainbow = "OrbitraRainbow";
    private static readonly ProtoId<DamageTypePrototype> HeatDamage = "Heat";
    private static readonly ProtoId<DamageTypePrototype> ColdDamage = "Cold";
    private static readonly ProtoId<DamageTypePrototype> StarvationDamage = "Poison";

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraXenobiologySlimeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OrbitraXenobiologySlimeComponent, IngestingEvent>(OnIngesting);
        SubscribeLocalEvent<OrbitraXenobiologySlimeComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<OrbitraXenobiologySlimeComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<OrbitraXenobiologySlimeComponent, EntGotRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<OrbitraXenobiologySlimeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<OrbitraXenobiologySlimeComponent, OrbitraXenobiologyDivideDoAfterEvent>(OnDivideDoAfter);
        SubscribeLocalEvent<OrbitraXenobiologySlimeComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<OrbitraXenobiologySlimeComponent> ent, ref MapInitEvent args)
    {
        RefreshAppearance(ent);
        SetSuspended(ent, _container.IsEntityInContainer(ent));
    }

    private void OnIngesting(Entity<OrbitraXenobiologySlimeComponent> ent, ref IngestingEvent args)
    {
        if (ent.Comp.Consumed || !_mobState.IsAlive(ent))
            return;

        var protein = 0f;
        var nutriment = 0f;
        foreach (var (reagent, quantity) in args.Split.Contents)
        {
            switch (reagent.Prototype.Id)
            {
                case "Protein":
                case "UncookedAnimalProteins":
                    protein += quantity.Float();
                    break;
                case "Nutriment":
                    nutriment += quantity.Float();
                    break;
            }
        }

        ent.Comp.ProteinIntake += protein;
        ent.Comp.NutrimentIntake += nutriment;
        ent.Comp.Biomass = Math.Min(120f, ent.Comp.Biomass + protein + nutriment * 0.75f);

        // Orbitra-Edit: успокаиваем по факту проглоченной пищи, не ожидая её переработки желудком.
        if (protein + nutriment > 0)
        {
            ent.Comp.FeedingGrace = 20f;
            SetHostile(ent, false);
        }
    }

    private void OnMobStateChanged(Entity<OrbitraXenobiologySlimeComponent> ent, ref MobStateChangedEvent args)
    {
        RefreshAppearance(ent);
    }

    private void OnInserted(Entity<OrbitraXenobiologySlimeComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        SetSuspended(ent, true);
    }

    private void OnRemoved(Entity<OrbitraXenobiologySlimeComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        SetSuspended(ent, false);
    }

    private void OnGetVerbs(Entity<OrbitraXenobiologySlimeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.Adult || !_mobState.IsAlive(ent))
            return;

        var uid = ent.Owner;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("orbitra-xeno-divide"),
            Disabled = !CanDivide(uid, user),
            Message = Loc.GetString("orbitra-xeno-divide-requirements"),
            Act = () => TryStartDivision(uid, user),
        });
    }

    private void OnDivideDoAfter(Entity<OrbitraXenobiologySlimeComponent> ent, ref OrbitraXenobiologyDivideDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = TryDivide(ent, args.User);
    }

    private void OnExamined(Entity<OrbitraXenobiologySlimeComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var breed = _prototype.Index(ent.Comp.Breed);
        args.PushMarkup(Loc.GetString("orbitra-xeno-examine",
            ("breed", Loc.GetString(breed.Name)),
            ("generation", ent.Comp.Generation),
            ("stage", Loc.GetString(ent.Comp.Adult ? "orbitra-xeno-stage-adult" : "orbitra-xeno-stage-baby")),
            ("biomass", MathF.Round(ent.Comp.Biomass))));
        if (ent.Comp.Hostile)
            args.PushMarkup(Loc.GetString("orbitra-xeno-examine-hungry"));
    }

    /// <summary>Starts a cancellable division interaction for a conscious handler in reach.</summary>
    public bool TryStartDivision(EntityUid slime, EntityUid user)
    {
        if (!CanDivide(slime, user))
            return false;

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(3),
            new OrbitraXenobiologyDivideDoAfterEvent(), slime, target: slime)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1.5f,
        });
    }

    /// <summary>Checks nutrition, adulthood, containment, player control and the colony population limit.</summary>
    public bool CanDivide(EntityUid slime, EntityUid? user = null)
    {
        // Orbitra edit start: индукция закрепляет взрослого слайма за промышленным циклом.
        if (HasComp<OrbitraXenobiologyIndustrialInductionComponent>(slime))
            return false;
        // Orbitra edit end

        if (TerminatingOrDeleted(slime) || !_slimeQuery.TryComp(slime, out var state) ||
            !state.Adult || state.Consumed || state.Suspended || !_mobState.IsAlive(slime) ||
            _container.IsEntityInContainer(slime) || HasPlayerMind(slime) ||
            !_satiationQuery.TryComp(slime, out var satiation) ||
            (_satiation.GetValueOrNull((slime, satiation), SatiationSystem.Hunger) ?? 0) < DivisionHunger)
            return false;

        var breed = _prototype.Index(state.Breed);
        if (state.Biomass < breed.DivisionBiomass || !IsComfortable(slime, state, breed))
            return false;

        if (user is { } handler &&
            (TerminatingOrDeleted(handler) || !_actionBlocker.CanInteract(handler, slime) ||
             !_interaction.InRangeUnobstructed(handler, slime)))
            return false;

        var transform = _transformQuery.GetComponent(slime);
        var count = 0;
        var query = EntityQueryEnumerator<OrbitraXenobiologySlimeComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var otherTransform))
        {
            if (otherTransform.MapID == transform.MapID)
                count++;
        }

        return count - 1 + Math.Clamp(state.Offspring, 2, 4) <= PopulationLimit;
    }

    /// <summary>Consumes one adult and creates a bounded litter carrying independently sampled inherited traits.</summary>
    public bool TryDivide(EntityUid slime, EntityUid? user = null)
    {
        if (!CanDivide(slime, user) || !_slimeQuery.TryComp(slime, out var parent))
            return false;

        var breed = _prototype.Index(parent.Breed);
        var coordinates = Transform(slime).Coordinates;
        var offspring = Math.Clamp(parent.Offspring, 2, 4);

        // Orbitra-Edit: закрываем повторное деление до любых спавнов и событий контейнеров.
        parent.Consumed = true;
        SetSuspended(slime, true);

        for (var i = 0; i < offspring; i++)
        {
            var childBreed = SelectChildBreed(parent, breed);
            var child = Spawn(_prototype.Index(childBreed).Mob, coordinates);
            var childState = _slimeQuery.GetComponent(child);
            childState.Generation = parent.Generation + 1;
            childState.MutationChance = parent.MutationChance is <= 0 or >= 1
                ? Math.Clamp(parent.MutationChance, 0, 1)
                : Math.Clamp(parent.MutationChance + _random.NextFloat(-0.025f, 0.025f), 0, 1);
            childState.GrowthRate = Math.Clamp(parent.GrowthRate * _random.NextFloat(0.95f, 1.05f), 0.5f, 2f);
            childState.Offspring = offspring;
            childState.ExtractYield = Math.Clamp(parent.ExtractYield, 1, 3);
            childState.Docility = Math.Clamp(parent.Docility, 0, 1);
            childState.ThermalTolerance = Math.Clamp(parent.ThermalTolerance, 0, 20);
        }

        if (user is { } handler)
            _popup.PopupEntity(Loc.GetString("orbitra-xeno-divided"), slime, handler);
        QueueDel(slime);
        return true;
    }

    /// <summary>Checks whether a corpse can produce extracts; juveniles and player-controlled specimens cannot.</summary>
    public bool CanHarvest(EntityUid slime)
    {
        // Orbitra edit start: индуцированное ядро нельзя собрать повторно.
        return !TerminatingOrDeleted(slime) && _slimeQuery.TryComp(slime, out var state) &&
               state.Adult && !state.Consumed && _mobState.IsDead(slime) && !HasPlayerMind(slime) &&
               !HasComp<OrbitraXenobiologyIndustrialInductionComponent>(slime);
        // Orbitra edit end
    }

    /// <summary>Claims an adult corpse exactly once. The caller spawns the returned yield and deletes the corpse.</summary>
    public bool TryHarvest(EntityUid slime, out ProtoId<OrbitraXenobiologyBreedPrototype> breed, out int yield)
    {
        breed = default;
        yield = 0;
        if (!CanHarvest(slime) || !_slimeQuery.TryComp(slime, out var state))
            return false;

        state.Consumed = true;
        SetSuspended(slime, true);
        breed = state.Breed;
        yield = Math.Clamp(state.ExtractYield, 1, 3);
        return true;
    }

    /// <summary>Checks whether a genetic treatment can change a living, unconsumed specimen.</summary>
    public bool CanApplyTreatment(EntityUid slime, OrbitraXenobiologyTreatment treatment)
    {
        if (TerminatingOrDeleted(slime) || !_slimeQuery.TryComp(slime, out var state) ||
            state.Consumed || !_mobState.IsAlive(slime))
            return false;

        return treatment switch
        {
            OrbitraXenobiologyTreatment.Mutation => state.MutationChance < 0.999f,
            OrbitraXenobiologyTreatment.Stability => state.MutationChance > 0.001f,
            OrbitraXenobiologyTreatment.Growth => state.GrowthRate < 1.999f,
            OrbitraXenobiologyTreatment.Yield => state.ExtractYield < 3,
            OrbitraXenobiologyTreatment.Fertility => state.Offspring < 4,
            OrbitraXenobiologyTreatment.Docility => state.Docility < 0.999f,
            OrbitraXenobiologyTreatment.Thermal => state.ThermalTolerance < 20f,
            _ => false,
        };
    }

    /// <summary>Applies a bounded, inheritable trait change. The caller consumes the successful treatment item.</summary>
    public bool TryApplyTreatment(EntityUid slime, OrbitraXenobiologyTreatment treatment)
    {
        if (!CanApplyTreatment(slime, treatment) || !_slimeQuery.TryComp(slime, out var state))
            return false;

        switch (treatment)
        {
            case OrbitraXenobiologyTreatment.Mutation:
                state.MutationChance = Math.Min(1, state.MutationChance + 0.1f);
                break;
            case OrbitraXenobiologyTreatment.Stability:
                state.MutationChance = Math.Max(0, state.MutationChance - 0.1f);
                break;
            case OrbitraXenobiologyTreatment.Growth:
                state.GrowthRate = Math.Min(2, state.GrowthRate + 0.15f);
                break;
            case OrbitraXenobiologyTreatment.Yield:
                state.ExtractYield++;
                break;
            case OrbitraXenobiologyTreatment.Fertility:
                state.Offspring++;
                break;
            case OrbitraXenobiologyTreatment.Docility:
                state.Docility = Math.Min(1, state.Docility + 0.25f);
                SetHostile((slime, state), false);
                break;
            case OrbitraXenobiologyTreatment.Thermal:
                state.ThermalTolerance = Math.Min(20, state.ThermalTolerance + 5f);
                break;
        }

        return true;
    }

    /// <summary>Stops development and current HTN actions while a specimen is held inside a container.</summary>
    public void SetSuspended(EntityUid slime, bool suspended)
    {
        if (!_slimeQuery.TryComp(slime, out var state))
            return;

        state.Suspended = suspended;
        if (!_htnQuery.TryComp(slime, out var htn))
            return;

        if (suspended)
        {
            state.ResumeHtn |= htn.Enabled;
            _htn.SetHTNEnabled((slime, htn), false);
        }
        else if (state.ResumeHtn && !state.Consumed)
        {
            state.ResumeHtn = false;
            _htn.SetHTNEnabled((slime, htn), true, 0.2f);
        }
    }

    /// <summary>Completes maturation only when sufficient real nutrition and growth have accumulated.</summary>
    public bool TryMature(EntityUid slime)
    {
        if (!CanMature(slime) || !_slimeQuery.TryComp(slime, out var state))
            return false;

        var breed = _prototype.Index(state.Breed);
        state.Biomass -= breed.GrowthBiomass;
        state.Adult = true;
        RefreshAppearance((slime, state));
        return true;
    }

    /// <summary>Checks maturation without mutating food reserves or the appearance.</summary>
    public bool CanMature(EntityUid slime)
    {
        if (TerminatingOrDeleted(slime) || !_slimeQuery.TryComp(slime, out var state) ||
            state.Adult || state.Consumed || state.Suspended || !_mobState.IsAlive(slime) ||
            _container.IsEntityInContainer(slime))
            return false;

        var breed = _prototype.Index(state.Breed);
        return state.Growth >= breed.GrowthDuration && state.Biomass >= breed.GrowthBiomass &&
               GetHunger(slime) >= MinimumGrowthHunger && IsComfortable(slime, state, breed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        var elapsed = _accumulator;
        _accumulator = 0;
        var query = EntityQueryEnumerator<OrbitraXenobiologySlimeComponent, TemperatureComponent>();
        while (query.MoveNext(out var uid, out var state, out var temperature))
        {
            if (state.Consumed || state.Suspended || !_mobState.IsAlive(uid))
                continue;

            var hunger = GetHunger(uid);
            var breed = _prototype.Index(state.Breed);
            var comfortable = IsComfortable(uid, state, breed);

            if (temperature.Temperature < 290.15f)
                state.ColdExposure += elapsed;
            else if (temperature.Temperature > 302.15f)
                state.WarmExposure += elapsed;

            state.FeedingGrace = Math.Max(0, state.FeedingGrace - elapsed);
            SetHostile((uid, state), state.FeedingGrace <= 0 && hunger < 10f * (1f - state.Docility));
            if (!state.Adult && state.Biomass > 0 && hunger >= MinimumGrowthHunger && comfortable)
            {
                state.Growth += elapsed * Math.Clamp(state.GrowthRate, 0.5f, 2f);
                TryMature(uid);
            }

            ApplyEnvironmentalDamage(uid, state, temperature.Temperature, hunger, elapsed);
        }
    }

    private bool IsComfortable(EntityUid uid, OrbitraXenobiologySlimeComponent state, OrbitraXenobiologyBreedPrototype breed)
    {
        return _temperatureQuery.TryComp(uid, out var temperature) &&
               temperature.Temperature >= breed.MinimumTemperature - state.ThermalTolerance &&
               temperature.Temperature <= breed.MaximumTemperature + state.ThermalTolerance;
    }

    private float GetHunger(EntityUid uid)
    {
        return _satiationQuery.TryComp(uid, out var satiation)
            ? _satiation.GetValueOrNull((uid, satiation), SatiationSystem.Hunger) ?? 0f
            : 0f;
    }

    private bool HasPlayerMind(EntityUid uid)
    {
        return TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind;
    }

    private void RefreshAppearance(Entity<OrbitraXenobiologySlimeComponent> ent)
    {
        var dead = _mobState.IsDead(ent);
        var stage = ent.Comp.Adult
            ? dead ? OrbitraXenobiologyStage.DeadAdult : OrbitraXenobiologyStage.Adult
            : dead ? OrbitraXenobiologyStage.DeadBaby : OrbitraXenobiologyStage.Baby;
        _appearance.SetData(ent, OrbitraXenobiologyVisuals.Stage, stage);
    }

    private void SetHostile(Entity<OrbitraXenobiologySlimeComponent> ent, bool hostile)
    {
        if (ent.Comp.Hostile == hostile)
            return;

        ent.Comp.Hostile = hostile;
        _npcFaction.RemoveFaction((ent, null), hostile ? CalmFaction : HungryFaction);
        _npcFaction.AddFaction((ent, null), hostile ? HungryFaction : CalmFaction);

        // Orbitra-Edit: перепланирование само не останавливает уже начатую атаку.
        if (_htnQuery.TryComp(ent, out var htn) && htn.Enabled)
        {
            _htn.SetHTNEnabled((ent, htn), false);
            if (!ent.Comp.Suspended && !ent.Comp.Consumed)
                _htn.SetHTNEnabled((ent, htn), true, 0.2f);
        }
    }

    private void ApplyEnvironmentalDamage(EntityUid uid, OrbitraXenobiologySlimeComponent state, float temperature,
        float hunger, float elapsed)
    {
        var damage = new DamageSpecifier();
        if (temperature < 260.15f - state.ThermalTolerance)
            damage.DamageDict[ColdDamage] = FixedPoint2.New(elapsed * 0.5f);
        if (temperature > 330.15f + state.ThermalTolerance)
            damage.DamageDict[HeatDamage] = FixedPoint2.New(elapsed * 0.5f);
        if (hunger <= 0)
            damage.DamageDict[StarvationDamage] = FixedPoint2.New(elapsed * 0.1f);
        if (!damage.Empty)
            _damageable.TryChangeDamage((uid, null), damage, interruptsDoAfters: false);
    }

    private ProtoId<OrbitraXenobiologyBreedPrototype> SelectChildBreed(OrbitraXenobiologySlimeComponent parent,
        OrbitraXenobiologyBreedPrototype breed)
    {
        if (parent.MutationChance >= 0.999f && breed.Tier >= 3)
            return Rainbow;

        var totalFood = parent.ProteinIntake + parent.NutrimentIntake;
        var mixedDiet = totalFood > 0
            ? Math.Min(parent.ProteinIntake, parent.NutrimentIntake) / totalFood
            : 0f;
        var thermalExposure = parent.ColdExposure + parent.WarmExposure;
        var chance = Math.Clamp(parent.MutationChance *
                               (1f + mixedDiet * 0.2f + Math.Min(0.1f, thermalExposure / 3600f)), 0, 1);
        if (!_random.Prob(chance) || breed.Mutations.Count == 0)
            return parent.Breed;

        var weightTotal = 0f;
        foreach (var (candidate, weight) in breed.Mutations)
            weightTotal += GetMutationWeight(candidate, weight, parent);
        if (weightTotal <= 0)
            return parent.Breed;

        var roll = _random.NextFloat() * weightTotal;
        foreach (var (candidate, weight) in breed.Mutations)
        {
            roll -= GetMutationWeight(candidate, weight, parent);
            if (roll <= 0)
                return candidate;
        }

        return parent.Breed;
    }

    private static float GetMutationWeight(ProtoId<OrbitraXenobiologyBreedPrototype> candidate, float weight,
        OrbitraXenobiologySlimeComponent parent)
    {
        var coldBiased = candidate.Id is "OrbitraBlue" or "OrbitraDarkBlue" or "OrbitraSilver" or "OrbitraCerulean" or "OrbitraSepia";
        var warmBiased = candidate.Id is "OrbitraOrange" or "OrbitraYellow" or "OrbitraRed" or "OrbitraOil" or "OrbitraDarkPurple";
        var proteinBiased = candidate.Id is "OrbitraRed" or "OrbitraGreen" or "OrbitraDarkPurple" or "OrbitraGold";
        var plantBiased = candidate.Id is "OrbitraPurple" or "OrbitraPink" or "OrbitraGreen" or "OrbitraLightPink";
        var adjusted = Math.Max(0, weight);
        if (coldBiased && parent.ColdExposure > parent.WarmExposure + 30f ||
            warmBiased && parent.WarmExposure > parent.ColdExposure + 30f)
            adjusted *= 2f;
        if (proteinBiased && parent.ProteinIntake > parent.NutrimentIntake * 1.5f ||
            plantBiased && parent.NutrimentIntake > parent.ProteinIntake * 1.5f)
            adjusted *= 1.5f;
        return adjusted;
    }
}
