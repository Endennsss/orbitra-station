using System.Numerics;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Construction.Components;
using Content.Shared.Materials;
using Content.Server.Station.Systems;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Projection reservations, actual damage, body ownership and interruption cleanup.</summary>
[TestFixture]
public sealed class OrbitraRatvarCrystalTest : GameTest
{
    private static readonly EntProtoId Rule = "OrbitraRatvarRule";
    private static readonly EntProtoId Human = "MobHuman";
    private static readonly EntProtoId Crystal = "OrbitraRatvarCrystal";
    private static readonly EntProtoId Role = "OrbitraMindRoleRatvar";
    private static readonly EntProtoId Crowbar = "Crowbar";
    private static readonly EntProtoId Backpack = "ClothingBackpack";
    private static readonly EntProtoId Helmet = "OrbitraRatvarHelmet";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };
    private Entity<OrbitraRatvarCrystalComponent> _source;
    private Entity<OrbitraRatvarCrystalComponent> _target;
    private EntityUid _body;
    private EntityUid _mind;
    private EntityUid _cult;
    private EntityCoordinates _origin;
    private OrbitraRatvarCrystalSystem System => Server.System<OrbitraRatvarCrystalSystem>();

    private async Task Prepare()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            var grid = SEntMan.GetComponent<MapGridComponent>(map.Grid);
            for (var x = 0; x <= 16; x++)
                Server.System<SharedMapSystem>().SetTile((map.Grid, grid), new Vector2i(x, 0), map.Tile.Tile);
            _origin = map.GridCoords.Offset(new Vector2(0.5f));
            Server.System<GameTicker>().StartGameRule(Rule, out _cult);
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationDataComponent>(station);
            Server.System<StationSystem>().AddGridToStation(station, map.Grid);
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_cult);
            cult.Station = station;
            _body = SEntMan.SpawnEntity(Human, _origin);
            _mind = Server.System<MindSystem>().CreateMind(null);
            Server.System<MindSystem>().TransferTo(_mind, _body);
            var roles = Server.System<RoleSystem>();
            roles.MindAddRole(_mind, Role);
            roles.MindHasRole<OrbitraRatvarRoleComponent>(_mind, out var role);
            role!.Value.Comp2.Rule = _cult;
            cult.Members.Add(_mind);
            var source = SEntMan.SpawnEntity(Crystal, _origin);
            var target = SEntMan.SpawnEntity(Crystal, _origin.Offset(new Vector2(8, 0)));
            _source = (source, SEntMan.GetComponent<OrbitraRatvarCrystalComponent>(source));
            _target = (target, SEntMan.GetComponent<OrbitraRatvarCrystalComponent>(target));
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(source).Rule = _cult;
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(target).Rule = _cult;
        });
    }

    [Test]
    public async Task AppearanceIsAnIndependentSnapshot()
    {
        await Prepare();
        var original = new OrganProfileData { Sex = Sex.Female, SkinColor = Color.Brown, EyeColor = Color.Green };
        await Server.WaitPost(() =>
        {
            Server.System<SharedVisualBodySystem>().ApplyProfile(_body, original);
            System.TryProject(_source, _body, _target);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(_target.Comp.Projection, Is.Not.Null);
            var visual = Server.System<SharedVisualBodySystem>();
            Assert.That(visual.TryGatherMarkingsData(_body, null, out var sourceProfiles, out _, out var sourceMarks), Is.True);
            Assert.That(visual.TryGatherMarkingsData(_target.Comp.Projection!.Value, null, out var profiles, out _, out var marks), Is.True);
            foreach (var (category, profile) in sourceProfiles!)
            {
                Assert.That(profiles![category], Is.EqualTo(profile));
                if (sourceMarks!.TryGetValue(category, out var sourceLayers))
                {
                    Assert.That(marks![category], Is.Not.SameAs(sourceLayers));
                    foreach (var (layer, sourceList) in sourceLayers)
                    {
                        Assert.That(marks[category][layer], Is.EqualTo(sourceList));
                        Assert.That(marks[category][layer], Is.Not.SameAs(sourceList));
                    }
                }
            }
        });
        await Server.WaitPost(() => Server.System<SharedVisualBodySystem>().ApplyProfile(_target.Comp.Projection!.Value,
            new OrganProfileData { SkinColor = Color.Blue, EyeColor = Color.Red }));
        await Server.WaitAssertion(() =>
        {
            Server.System<SharedVisualBodySystem>().TryGatherMarkingsData(_body, null, out var profiles, out _, out _);
            foreach (var profile in profiles!.Values)
                Assert.That(profile, Is.EqualTo(original));
        });
    }

    [Test]
    public async Task CatalogueIsPrivateAndRejectsStaleDestinations()
    {
        await Prepare();
        bool renamed = false, invalid = true, tooLong = true, stolen = true, projected = true;
        OrbitraRatvarCrystalUiState? state = null, foreign = null, stale = null;
        await Server.WaitPost(() =>
        {
            renamed = System.TryRenameCrystal(_source, _body, "  Base  ");
            invalid = System.TryRenameCrystal(_source, _body, "bad\nname");
            tooLong = System.TryRenameCrystal(_source, _body, new string('x', 41));
            state = System.BuildCrystalState(_source, _body);
            var stranger = SEntMan.SpawnEntity(Human, _origin);
            foreign = System.BuildCrystalState(_source, stranger);
            stolen = System.TryRenameCrystal(_source, stranger, "Stolen");
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(_target).Rule = null;
            stale = System.BuildCrystalState(_source, _body);
            projected = System.TryProject(_source, _body, _target);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(renamed, Is.True);
            Assert.That(invalid, Is.False);
            Assert.That(tooLong, Is.False);
            Assert.That(state!.Name, Is.EqualTo("Base"));
            Assert.That(state.Destinations.Length, Is.EqualTo(1));
            Assert.That(state.Destinations[0].Reason, Is.EqualTo("orbitra-ratvar-crystal-point-ready"));
            Assert.That(foreign, Is.Null);
            Assert.That(stolen, Is.False);
            Assert.That(stale!.Destinations, Is.Empty);
            Assert.That(projected, Is.False);
        });
    }

    [Test]
    public async Task VisitKeepsMembershipAndReservesDestination()
    {
        await Prepare();
        var started = false;
        var repeated = true;
        await Server.WaitPost(() =>
        {
            started = System.TryProject(_source, _body, _target);
            repeated = System.TryProject(_source, _body, _target);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(repeated, Is.False);
            var mind = SEntMan.GetComponent<MindComponent>(_mind);
            Assert.That(mind.OwnedEntity, Is.EqualTo(_body));
            Assert.That(mind.VisitingEntity, Is.EqualTo(_target.Comp.Projection));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarProjectionVisualComponent>(mind.VisitingEntity!.Value).Anchor,
                Is.EqualTo(_target.Owner));
            Assert.That(Server.System<OrbitraRatvarRuleSystem>().TryGetCult(mind.VisitingEntity!.Value, out var rule), Is.True);
            Assert.That(rule.Owner, Is.EqualTo(_cult));
            Assert.That(rule.Comp.Members.Count, Is.EqualTo(1));
        });
        await Server.WaitPost(() => Server.System<MindSystem>().UnVisit(_mind));
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(_target.Comp.Projection, Is.Null));
    }

    [TestCase("target")]
    [TestCase("source")]
    [TestCase("projection")]
    [TestCase("body-death")]
    [TestCase("body-deleted")]
    [TestCase("projection-critical")]
    [TestCase("range")]
    [TestCase("transfer")]
    [TestCase("purify")]
    [TestCase("grid")]
    public async Task InterruptionReturnsOnlyTheOwnedVisit(string reason)
    {
        await Prepare();
        EntityUid projection = default;
        EntityUid[] equipment = [];
        EntityUid? expectedBody = _body;
        await Server.WaitPost(() => System.TryProject(_source, _body, _target));
        await Server.WaitAssertion(() => Assert.That(_target.Comp.Projection, Is.Not.Null));
        await Server.WaitPost(() =>
        {
            projection = _target.Comp.Projection!.Value;
            equipment = [.. SEntMan.GetComponent<ActiveOrbitraRatvarProjectionComponent>(projection).Equipment];
            switch (reason)
            {
                case "grid":
                    expectedBody = null;
                    SEntMan.DeleteEntity(_origin.EntityId);
                    break;
                case "target": SEntMan.DeleteEntity(_target); break;
                case "source": SEntMan.DeleteEntity(_source); break;
                case "projection": SEntMan.DeleteEntity(projection); break;
                case "body-death": Server.System<MobStateSystem>().ChangeMobState(_body, MobState.Dead); break;
                case "body-deleted":
                    expectedBody = null;
                    SEntMan.DeleteEntity(_body);
                    break;
                case "projection-critical": Server.System<MobStateSystem>().ChangeMobState(projection, MobState.Critical); break;
                case "range": Server.System<SharedTransformSystem>().SetCoordinates(projection, _origin.Offset(new Vector2(14, 0))); break;
                case "transfer":
                    expectedBody = SEntMan.SpawnEntity(Human, _origin);
                    Server.System<MindSystem>().TransferTo(_mind, expectedBody);
                    break;
                case "purify":
                    Server.System<OrbitraRatvarRuleSystem>().TryPurify((_cult, SEntMan.GetComponent<OrbitraRatvarRuleComponent>(_cult)), _mind);
                    break;
            }
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var mind = SEntMan.GetComponent<MindComponent>(_mind);
            Assert.That(mind.VisitingEntity, Is.Null);
            Assert.That(mind.OwnedEntity, Is.EqualTo(expectedBody));
            Assert.That(SEntMan.Deleted(projection), Is.True);
            Assert.That(equipment.Length, Is.EqualTo(3));
            foreach (var item in equipment) Assert.That(SEntMan.Deleted(item), Is.True);
            if (reason != "target" && reason != "grid") Assert.That(_target.Comp.Projection, Is.Null);
        });
    }

    [Test]
    public async Task ConnectedSessionFollowsVisitAndRespectsAdministrativeTransfer()
    {
        await Prepare();
        EntityUid replacement = default;
        await Server.WaitPost(() =>
        {
            Server.System<MindSystem>().SetUserId(_mind, ServerSession!.UserId);
            System.TryProject(_source, _body, _target);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_target.Comp.Projection, Is.Not.Null);
            Assert.That(ServerSession!.AttachedEntity, Is.EqualTo(_target.Comp.Projection));
        });
        await Server.WaitPost(() =>
        {
            replacement = SEntMan.SpawnEntity(Human, _origin);
            Server.System<MindSystem>().TransferTo(_mind, replacement);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(ServerSession!.AttachedEntity, Is.EqualTo(replacement));
            Assert.That(SEntMan.GetComponent<MindComponent>(_mind).OwnedEntity, Is.EqualTo(replacement));
            Assert.That(_target.Comp.Projection, Is.Null);
        });
    }

    [TestCase(1f)]
    [TestCase(2f)]
    public async Task DamageSplitsOnceAndPickedUpItemsSurviveReturn(float multiplier)
    {
        await Prepare();
        EntityUid projection = default, item = default;
        await Server.WaitPost(() => System.TryProject(_source, _body, _target));
        await Server.WaitAssertion(() => Assert.That(_target.Comp.Projection, Is.Not.Null));
        await Server.WaitPost(() =>
        {
            Server.Resolve<IConfigurationManager>().SetCVar(CCVars.PlaytestAllDamageModifier, multiplier);
            projection = _target.Comp.Projection!.Value;
            Server.System<DamageableSystem>().TryChangeDamage(projection,
                new DamageSpecifier { DamageDict = { [Blunt] = 10 } }, ignoreResistances: true);
            item = SEntMan.SpawnEntity(Crowbar, SEntMan.GetComponent<TransformComponent>(projection).Coordinates);
            Server.System<SharedHandsSystem>().TryPickupAnyHand(projection, item);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<DamageableSystem>().GetPositiveDamage((_body, SEntMan.GetComponent<DamageableComponent>(_body))).GetTotal().Float(), Is.EqualTo(4 * multiplier));
            Assert.That(Server.System<DamageableSystem>().GetPositiveDamage((_target.Owner, SEntMan.GetComponent<DamageableComponent>(_target))).GetTotal().Float(), Is.EqualTo(6 * multiplier));
            Assert.That(Server.System<SharedHandsSystem>().IsHolding(projection, item), Is.True);
        });
        await Server.WaitPost(() =>
        {
            var active = SEntMan.GetComponent<ActiveOrbitraRatvarProjectionComponent>(projection);
            System.EndProjection((projection, active));
            System.EndProjection((projection, active));
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(item), Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(item).ParentUid, Is.Not.EqualTo(projection));
            Assert.That(SEntMan.GetComponent<MindComponent>(_mind).VisitingEntity, Is.Null);
        });
    }

    [TestCase("foreign")]
    [TestCase("unanchored")]
    [TestCase("same")]
    public async Task InvalidDestinationDoesNotCreateVisit(string reason)
    {
        await Prepare();
        var started = true;
        await Server.WaitPost(() =>
        {
            if (reason == "foreign")
            {
                Server.System<GameTicker>().StartGameRule(Rule, out var other);
                SEntMan.GetComponent<OrbitraRatvarStructureComponent>(_target).Rule = other;
            }
            if (reason == "unanchored") Server.System<SharedTransformSystem>().Unanchor(_target);
            started = System.TryProject(_source, _body, reason == "same" ? _source.Owner : _target.Owner);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.False);
            Assert.That(_target.Comp.Projection, Is.Null);
            Assert.That(SEntMan.GetComponent<MindComponent>(_mind).VisitingEntity, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task TemporaryEquipmentSurvivesDropButNotAnotherHolder(bool nested)
    {
        await Prepare();
        EntityUid projection = default, spear = default, bag = default, other = default;
        var inserted = false;
        await Server.WaitPost(() =>
        {
            System.TryProject(_source, _body, _target);
            projection = _target.Comp.Projection!.Value;
            spear = Server.System<SharedHandsSystem>().GetActiveItem(projection)!.Value;
            if (nested)
            {
                Server.System<InventorySystem>().TryGetSlotEntity(projection, "head", out var helmet);
                spear = helmet!.Value;
            }
            Server.System<SharedContainerSystem>().TryRemoveFromContainer(spear, force: true);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() => Assert.That(SEntMan.EntityExists(spear), Is.True));
        await Server.WaitPost(() =>
        {
            var containers = Server.System<SharedContainerSystem>();
            other = SEntMan.SpawnEntity(Human, SEntMan.GetComponent<TransformComponent>(projection).Coordinates);
            if (nested)
            {
                bag = SEntMan.SpawnEntity(Backpack, SEntMan.GetComponent<TransformComponent>(projection).Coordinates);
                inserted = containers.Insert(spear, containers.GetContainer(bag, "storagebase"));
                Server.System<SharedHandsSystem>().TryPickupAnyHand(other, bag);
            }
            else inserted = Server.System<SharedHandsSystem>().TryPickupAnyHand(other, spear);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(inserted, Is.True);
            Assert.That(SEntMan.Deleted(spear), Is.True);
            Assert.That(_target.Comp.Projection, Is.EqualTo(projection));
            Assert.That(SEntMan.GetComponent<ActiveOrbitraRatvarProjectionComponent>(projection).Equipment.Count, Is.EqualTo(2));
            if (nested) Assert.That(SEntMan.EntityExists(bag), Is.True);
        });
    }

    [Test]
    public async Task EquippedArmamentsCannotProduceMaterialsAndRealReplacementSurvives()
    {
        await Prepare();
        EntityUid projection = default, realHelmet = default, spear = default;
        await Server.WaitPost(() =>
        {
            System.TryProject(_source, _body, _target);
            projection = _target.Comp.Projection!.Value;
            spear = Server.System<SharedHandsSystem>().GetActiveItem(projection)!.Value;
        });
        await Server.WaitAssertion(() =>
        {
            var inventory = Server.System<InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(projection, "head", out var helmet), Is.True);
            Assert.That(inventory.TryGetSlotEntity(projection, "outerClothing", out var armor), Is.True);
            Assert.That(SEntMan.HasComponent<OrbitraRatvarProjectionItemComponent>(helmet!.Value), Is.True);
            Assert.That(SEntMan.HasComponent<OrbitraRatvarProjectionItemComponent>(armor!.Value), Is.True);
            Assert.That(SEntMan.HasComponent<ConstructionComponent>(spear), Is.False);
            Assert.That(SEntMan.HasComponent<PhysicalCompositionComponent>(spear), Is.False);
        });
        var equipped = false;
        await Server.WaitPost(() =>
        {
            var inventory = Server.System<InventorySystem>();
            inventory.TryUnequip(projection, "head", silent: true, force: true);
            realHelmet = SEntMan.SpawnEntity(Helmet, SEntMan.GetComponent<TransformComponent>(projection).Coordinates);
            equipped = inventory.TryEquip(projection, realHelmet, "head", silent: true);
            Server.System<DamageableSystem>().TryChangeDamage(spear,
                new DamageSpecifier { DamageDict = { [Blunt] = 20 } }, ignoreResistances: true);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(equipped, Is.True);
            Assert.That(SEntMan.Deleted(spear), Is.True);
            var entities = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (entities.MoveNext(out _, out var metadata, out var transform))
                if (transform.GridUid == _origin.EntityId)
                    Assert.That(metadata.EntityPrototype?.ID, Is.Not.EqualTo("PartRodMetal1"));
        });
        await Server.WaitPost(() => Server.System<MindSystem>().UnVisit(_mind));
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(realHelmet), Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(realHelmet).ParentUid, Is.EqualTo(_origin.EntityId));
        });
    }

    [TestCase("return")]
    [TestCase("destroy-bag")]
    [TestCase("steal-bag")]
    public async Task TemporaryContainerPreservesRealContents(string reason)
    {
        await Prepare();
        EntityUid projection = default, bag = default, item = default;
        var inserted = false;
        await Server.WaitPost(() =>
        {
            _target.Comp.Equipment.Add("back", Backpack);
            System.TryProject(_source, _body, _target);
            projection = _target.Comp.Projection!.Value;
            Server.System<InventorySystem>().TryGetSlotEntity(projection, "back", out var equipped);
            bag = equipped!.Value;
            item = SEntMan.SpawnEntity(Crowbar, SEntMan.GetComponent<TransformComponent>(projection).Coordinates);
            var containers = Server.System<SharedContainerSystem>();
            inserted = containers.Insert(item, containers.GetContainer(bag, "storagebase"));
        });
        await Server.WaitAssertion(() => Assert.That(inserted, Is.True));
        await Server.WaitPost(() =>
        {
            if (reason == "destroy-bag") SEntMan.DeleteEntity(bag);
            else if (reason == "steal-bag")
            {
                var other = SEntMan.SpawnEntity(Human, SEntMan.GetComponent<TransformComponent>(projection).Coordinates);
                Server.System<InventorySystem>().TryUnequip(projection, "back", silent: true, force: true);
                Server.System<SharedHandsSystem>().TryPickupAnyHand(other, bag);
            }
            else Server.System<MindSystem>().UnVisit(_mind);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(bag), Is.True);
            Assert.That(SEntMan.EntityExists(item), Is.True);
            Assert.That(Server.System<SharedContainerSystem>().IsEntityInContainer(item), Is.False);
            Assert.That(SEntMan.GetComponent<TransformComponent>(item).ParentUid, Is.EqualTo(_origin.EntityId));
        });
    }
}
