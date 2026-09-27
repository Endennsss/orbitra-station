using Content.Server.Administration.Logs;
using Content.Server.Body.Components;
using Content.Server.Atmos.Components;
using Content.Server.Mind;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Components;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Station;
using Content.Shared.Nutrition.Components;
using Content.Shared.Temperature.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Map.Components;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Temporary, cult-isolated manifestation using native mind visits and reversible teardown.</summary>
public sealed partial class OrbitraRatvarCrystalSystem : EntitySystem
{
    [Dependency] private OrbitraRatvarRuleSystem _rule = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Structural = "Structural";
    private readonly HashSet<EntityUid> _crystals = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarCrystalComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<OrbitraRatvarCrystalComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OrbitraRatvarCrystalComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<OrbitraRatvarCrystalComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<ActiveOrbitraRatvarProjectionComponent, MindUnvisitedMessage>(OnUnvisited);
        SubscribeLocalEvent<ActiveOrbitraRatvarProjectionComponent, PlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<ActiveOrbitraRatvarProjectionComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<ActiveOrbitraRatvarProjectionComponent, DamageChangedEvent>(OnDamage);
        SubscribeLocalEvent<ActiveOrbitraRatvarProjectionComponent, GetVerbsEvent<AlternativeVerb>>(OnReturnVerb);
        InitializeEquipment();
        InitializeUi();
    }

    public override void Shutdown()
    {
        _crystals.Clear();
        base.Shutdown();
    }

    private void OnStartup(Entity<OrbitraRatvarCrystalComponent> ent, ref ComponentStartup args) => _crystals.Add(ent);

    private void OnExamine(Entity<OrbitraRatvarCrystalComponent> ent, ref ExaminedEvent args)
    {
        if (_rule.TryGetCult(args.Examiner, out var cult) && ValidCrystal(ent, cult))
            args.PushMarkup(Loc.GetString(ent.Comp.Projection == null ? "orbitra-ratvar-crystal-ready" : "orbitra-ratvar-crystal-busy"));
    }

    private void OnShutdown(Entity<OrbitraRatvarCrystalComponent> ent, ref ComponentShutdown args)
    {
        _crystals.Remove(ent);
        var query = EntityQueryEnumerator<ActiveOrbitraRatvarProjectionComponent>();
        while (query.MoveNext(out var uid, out var projection))
            if (projection.Crystal == ent.Owner || projection.Source == ent.Owner)
                EndProjection((uid, projection));
    }

    private void OnVerbs(Entity<OrbitraRatvarCrystalComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract) return;
        var user = args.User;
        if (TryComp<ActiveOrbitraRatvarProjectionComponent>(user, out var active))
        {
            if (active.Crystal == ent.Owner)
                args.Verbs.Add(new AlternativeVerb { Text = Loc.GetString("orbitra-ratvar-crystal-return"), Act = () => EndProjection((user, active)) });
            return;
        }
    }

    private void OnReturnVerb(Entity<ActiveOrbitraRatvarProjectionComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (args.User != ent.Owner) return;
        args.Verbs.Add(new AlternativeVerb { Text = Loc.GetString("orbitra-ratvar-crystal-return"), Act = () => EndProjection(ent) });
    }

    private void OnUnvisited(Entity<ActiveOrbitraRatvarProjectionComponent> ent, ref MindUnvisitedMessage args) => EndProjection(ent);
    private void OnDetached(Entity<ActiveOrbitraRatvarProjectionComponent> ent, ref PlayerDetachedEvent args) => EndProjection(ent);
    private void OnTerminating(Entity<ActiveOrbitraRatvarProjectionComponent> ent, ref EntityTerminatingEvent args) => EndProjection(ent);

    private void OnDamage(Entity<ActiveOrbitraRatvarProjectionComponent> ent, ref DamageChangedEvent args)
    {
        // Нужна фактическая дельта после модели урона; DamageDealtEvent вызывается до её применения.
        if (ent.Comp.Ending || args.DamageDelta == null || !args.DamageIncreased ||
            !TryComp<OrbitraRatvarCrystalComponent>(ent.Comp.Crystal, out var crystal)) return;
        var amount = Content.Shared.FixedPoint.FixedPoint2.Zero;
        foreach (var damage in args.DamageDelta.DamageDict.Values)
            if (damage > 0) amount += damage;
        if (amount <= 0) return;
        // Повторно не применяем сопротивления к уже фактически полученному урону.
        if (!TerminatingOrDeleted(ent.Comp.Body))
            _damageable.TryChangeDamage(ent.Comp.Body, new DamageSpecifier { DamageDict = { [Blunt] = amount * crystal.BodyDamageFraction } },
                ignoreResistances: true, origin: args.Origin, ignoreGlobalModifiers: true);
        if (!TerminatingOrDeleted(ent.Comp.Crystal))
            _damageable.TryChangeDamage(ent.Comp.Crystal, new DamageSpecifier { DamageDict = { [Structural] = amount * crystal.CrystalDamageFraction } },
                ignoreResistances: true, origin: args.Origin, ignoreGlobalModifiers: true);
    }

    /// <summary>Checks a controlled living human, two own station crystals and an unreserved destination.</summary>
    public bool CanProject(Entity<OrbitraRatvarCrystalComponent> source, EntityUid user, EntityUid target,
        out EntityUid mind, out EntityUid cult)
    {
        mind = default;
        cult = default;
        if (source.Owner == target || !Living(user) || !TryComp<HumanoidProfileComponent>(user, out var profile) || profile.Species != "Human" ||
            HasComp<ActiveOrbitraRatvarProjectionComponent>(user) || _container.IsEntityInContainer(user) ||
            !_mind.TryGetMind(user, out mind, out var mindComp) || mindComp.OwnedEntity != user || mindComp.VisitingEntity != null ||
            !_rule.TryGetCult(user, out var rule) || !ValidCrystal(source, rule) || !ValidCrystal(target, rule) ||
            !TryComp<OrbitraRatvarCrystalComponent>(target, out var crystal) || crystal.Projection != null ||
            !_actionBlocker.CanInteract(user, source) || !_interaction.InRangeUnobstructed(user, source.Owner)) return false;
        cult = rule;
        return true;
    }

    /// <summary>Reserves the destination before creating a visit; never transfers body ownership or copies inventory.</summary>
    public bool TryProject(Entity<OrbitraRatvarCrystalComponent> source, EntityUid user, EntityUid target)
    {
        if (!CanProject(source, user, target, out var mind, out var rule))
        {
            _popup.PopupEntity(Loc.GetString("orbitra-ratvar-crystal-denied"), user, user);
            return false;
        }
        var crystal = Comp<OrbitraRatvarCrystalComponent>(target);
        var body = Spawn(crystal.ProjectionPrototype, Transform(target).Coordinates);
        CopyProjectionAppearance(user, body);
        // Световая проекция не задыхается, но сохраняет штатные столкновения и зрение.
        RemComp<RespiratorComponent>(body);
        RemComp<BloodstreamComponent>(body);
        RemComp<BarotraumaComponent>(body);
        RemComp<TemperatureDamageComponent>(body);
        RemComp<SatiationComponent>(body);
        crystal.Projection = body;
        var active = AddComp<ActiveOrbitraRatvarProjectionComponent>(body);
        active.Body = user;
        active.Mind = mind;
        active.Source = source;
        active.Crystal = target;
        active.Rule = rule;
        var visual = EnsureComp<OrbitraRatvarProjectionVisualComponent>(body);
        visual.Anchor = target;
        Dirty(body, visual);
        EquipProjection((body, active), crystal);
        _metaData.SetEntityName(body, Loc.GetString("orbitra-ratvar-crystal-projection-name", ("name", Name(user))));
        _mind.Visit(mind, body);
        _ui.CloseUi(source.Owner, OrbitraRatvarCrystalUiKey.Key, user);
        _popup.PopupEntity(Loc.GetString("orbitra-ratvar-crystal-started"), body, body);
        _adminLog.Add(LogType.Mind, LogImpact.Medium, $"Ratvar crystal: {ToPrettyString(mind)} projects from {ToPrettyString(user)} at {ToPrettyString(target)}.");
        return true;
    }

    /// <summary>Idempotently returns only this visitor; an unrelated administrative mind transfer is never undone.</summary>
    public void EndProjection(Entity<ActiveOrbitraRatvarProjectionComponent> ent)
    {
        if (ent.Comp.Ending) return;
        ent.Comp.Ending = true;
        RemCompDeferred<OrbitraRatvarProjectionVisualComponent>(ent);
        if (TryComp<OrbitraRatvarCrystalComponent>(ent.Comp.Crystal, out var crystal) && crystal.Projection == ent.Owner)
            crystal.Projection = null;
        if (TryComp<MindComponent>(ent.Comp.Mind, out var mind) && mind.VisitingEntity == ent.Owner)
            _mind.UnVisit(ent.Comp.Mind, mind);
        // Отслеживаем идентификаторы выданных вещей, а не занятые ими слоты.
        foreach (var item in ent.Comp.Equipment)
            if (TryComp<OrbitraRatvarProjectionItemComponent>(item, out var equipment))
                DissolveEquipment((item, equipment));
        ent.Comp.Equipment.Clear();
        // Реальные вещи, подобранные проекцией, остаются на станции, а не удаляются с телом.
        // При удалении грида/карты сохранять вещи некуда: штатное удаление очистит дочерние сущности.
        if (!TerminatingOrDeleted(_transform.GetMoverCoordinates(ent).EntityId))
        {
            _hands.DropAll((ent.Owner, null), checkActionBlocker: false);
            if (_inventory.TryGetSlots(ent, out var slots))
                foreach (var slot in slots) _inventory.TryUnequip(ent, slot.Name, silent: true, force: true);
        }
        if (!TerminatingOrDeleted(ent)) QueueDel(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateEquipment();
        UpdateInterfaces();
        var query = EntityQueryEnumerator<ActiveOrbitraRatvarProjectionComponent>();
        while (query.MoveNext(out var uid, out var active))
        {
            if (active.Ending) continue;
            if (!Living(active.Body) || !Living(uid) ||
                !TryComp<MindComponent>(active.Mind, out var mind) || mind.OwnedEntity != active.Body || mind.VisitingEntity != uid ||
                !_rule.TryGetCult(active.Body, out var cult) || cult.Owner != active.Rule ||
                !ValidCrystal(active.Source, cult) || !ValidCrystal(active.Crystal, cult) ||
                !TryComp<OrbitraRatvarCrystalComponent>(active.Crystal, out var crystal) || crystal.Projection != uid ||
                _container.IsEntityInContainer(uid) || _container.IsEntityInContainer(active.Body) ||
                Transform(uid).GridUid != Transform(active.Crystal).GridUid ||
                !_transform.InRange(uid, active.Crystal, crystal.Radius))
                EndProjection((uid, active));
        }
    }

    private bool Living(EntityUid uid) => !TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid) &&
        TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Alive;

    private bool ValidCrystal(EntityUid uid, Entity<OrbitraRatvarRuleComponent> cult) =>
        !TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid) &&
        HasComp<OrbitraRatvarCrystalComponent>(uid) && Transform(uid).Anchored && !_container.IsEntityInContainer(uid) &&
        TryComp<OrbitraRatvarStructureComponent>(uid, out var structure) && structure.Rule == cult.Owner &&
        cult.Comp.Station is { } station && _station.GetOwningStation(uid) == station &&
        Transform(uid).GridUid is { } grid && _station.GetLargestGrid(station) == grid &&
        TryComp<MapGridComponent>(grid, out var mapGrid) && !_map.GetTileRef(grid, mapGrid, Transform(uid).Coordinates).Tile.IsEmpty;
}
