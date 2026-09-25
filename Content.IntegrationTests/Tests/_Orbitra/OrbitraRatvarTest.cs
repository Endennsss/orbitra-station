using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Antag;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Content.Server.Station.Systems;
using Content.Shared.Station.Components;
using System.Linq;
using System.Collections.Generic;
using Robust.Shared.Localization;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Timing;
using Content.Shared.Body.Components;
using Content.Client._Orbitra.Ratvar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Smoke coverage for the independent preset, costs and progression thresholds.</summary>
[TestFixture]
public sealed class OrbitraRatvarTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";
    private static readonly ProtoId<AntagSpecifierPrototype> OrbitraRatvarCultistPrototype = "OrbitraRatvarCultist";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [Test]
    public async Task AdminGrantCreatesCultAndRejectsRepeatedGrant()
    {
        var map = await Pair.CreateTestMap();
        EntityUid body = default;
        await Server.WaitPost(() =>
        {
            body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var minds = Server.System<MindSystem>();
            var mind = minds.CreateMind(ServerSession!.UserId);
            minds.TransferTo(mind, body);
            var admins = Server.ResolveDependency<Content.Server.Administration.Managers.IAdminManager>();
            admins.PromoteHost(ServerSession);
        });
        await Pair.RunTicksSync(2);

        var verbs = Server.System<Content.Server.Administration.Systems.AdminVerbSystem>();
        await Server.WaitAssertion(() =>
        {
            Assert.That(verbs.CanMakeOrbitraRatvarCultist(ServerSession!, body), Is.True);
            var menu = Server.System<Content.Server.Verbs.VerbSystem>().GetLocalVerbs(body, body, typeof(Content.Shared.Verbs.Verb));
            Assert.That(menu.Any(v => v.Text == Loc.GetString("orbitra-ratvar-admin-make") && !v.Disabled), Is.True);
        });
        var granted = false;
        await Server.WaitPost(() => granted = verbs.TryMakeOrbitraRatvarCultist(ServerSession!, body));
        await Server.WaitAssertion(() =>
        {
            Assert.That(granted, Is.True);
            Assert.That(Server.System<OrbitraRatvarRuleSystem>().TryGetCult(body, out var cult), Is.True);
            Assert.That(cult.Comp.Members.Count, Is.EqualTo(1));
            Assert.That(cult.Comp.Energy, Is.EqualTo(cult.Comp.StartingEnergy));
            Assert.That(SEntMan.EntityQuery<OrbitraRatvarTabletComponent>().Count(), Is.EqualTo(1));
            Assert.That(verbs.CanMakeOrbitraRatvarCultist(ServerSession!, body), Is.False);
        });
        await Server.WaitPost(() => granted = verbs.TryMakeOrbitraRatvarCultist(ServerSession!, body));
        await Server.WaitAssertion(() =>
        {
            Assert.That(granted, Is.False);
            Assert.That(SEntMan.EntityQuery<OrbitraRatvarTabletComponent>().Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task HolyWaterRequiresContinuousExposure()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<GameTicker>().StartGameRule(CultRule, out var rule), Is.True);
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            var system = Server.System<OrbitraRatvarRuleSystem>();
            var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var minds = Server.System<MindSystem>();
            var roles = Server.System<RoleSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, body);
            roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
            Assert.That(roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role), Is.True);
            role!.Value.Comp2.Rule = rule;
            cult.Members.Add(mind);
            SEntMan.EnsureComponent<OrbitraRatvarBodyComponent>(body);
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var bloodName = SEntMan.GetComponent<BloodstreamComponent>(body).BloodSolutionName;
            Assert.That(solutions.TryGetSolution(body, bloodName, out var chemicals, out _), Is.True);
            Assert.That(solutions.TryAddReagent(chemicals!.Value, cult.PurifyingReagent.Id, 50), Is.True);
            system.Update(0);
            Assert.That(cult.HolyWaterSince.ContainsKey(mind), Is.True);
            var now = Server.ResolveDependency<IGameTiming>().CurTime;
            cult.HolyWaterSince[mind] = now - TimeSpan.FromSeconds(29);
            cult.NextUpdate = TimeSpan.Zero;
            system.Update(0);
            Assert.That(roles.MindHasRole<OrbitraRatvarRoleComponent>(mind), Is.True);
            solutions.RemoveAllSolution(chemicals.Value);
            Assert.That(cult.HolyWaterSince.ContainsKey(mind), Is.False, "Even a same-tick interruption resets purification.");
            Assert.That(solutions.TryAddReagent(chemicals.Value, cult.PurifyingReagent.Id, 50), Is.True);
            cult.NextUpdate = TimeSpan.Zero;
            system.Update(0);
            Assert.That(cult.HolyWaterSince[mind], Is.EqualTo(now));
            cult.HolyWaterSince[mind] = now - TimeSpan.FromSeconds(30);
            cult.NextUpdate = TimeSpan.Zero;
            system.Update(0);
            Assert.That(roles.MindHasRole<OrbitraRatvarRoleComponent>(mind), Is.False);
            Assert.That(cult.ProtectedUntil[mind], Is.EqualTo(now + TimeSpan.FromMinutes(2)));
        });
    }

    [Test]
    public async Task TabletWindowUsesServerAvailability()
    {
        OrbitraRatvarWindow window = null!;
        await Client.WaitPost(() =>
        {
            window = new OrbitraRatvarWindow();
            window.Populate(Client.ResolveDependency<IPrototypeManager>().EnumeratePrototypes<OrbitraRatvarScripturePrototype>());
            window.OpenCentered();
            window.UpdateCult(new OrbitraRatvarUiState(0, 1, 0, false));
        });
        await Pair.RunTicksSync(5);
        await Client.WaitAssertion(() =>
        {
            var buttons = Descendants(window).OfType<Button>().Where(b => b.Text == Loc.GetString("orbitra-ratvar-scripture-brass")).ToArray();
            Assert.That(buttons, Has.Length.EqualTo(1));
            Assert.That(buttons[0].Disabled, Is.True);
            window.UpdateCult(new OrbitraRatvarUiState(1000, 3, 4, false));
            Assert.That(buttons[0].Disabled, Is.False);
            window.UpdateCult(new OrbitraRatvarUiState(1000, 3, 4, true));
            Assert.That(buttons[0].Disabled, Is.True);
            window.Close();
            window.Orphan();
        });
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        yield return control;
        foreach (var child in control.Children)
        foreach (var descendant in Descendants(child))
            yield return descendant;
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ArkActivationAndOutcome(bool destroy)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<OrbitraRatvarRuleSystem>();
            Assert.That(Server.System<GameTicker>().StartGameRule(CultRule, out var rule), Is.True);
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationDataComponent>(station);
            Server.System<StationSystem>().AddGridToStation(station, map.Grid);
            cult.Station = station;
            var ark = SEntMan.SpawnEntity("OrbitraRatvarArk", map.GridCoords);
            var structure = SEntMan.GetComponent<OrbitraRatvarStructureComponent>(ark);
            structure.Rule = rule;
            cult.Ark = ark;
            cult.Generated = cult.TierThreeEnergy;
            cult.Energy = cult.ArkEnergy;
            for (var i = 0; i < cult.TierThreeConverts; i++)
                cult.Converted.Add(SEntMan.SpawnEntity(null, MapCoordinates.Nullspace));
            var user = EntityUid.Invalid;
            for (var i = 0; i < cult.ArkCultists; i++)
            {
                user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                var mind = Server.System<MindSystem>().CreateMind(null);
                Server.System<MindSystem>().TransferTo(mind, user);
                Server.System<RoleSystem>().MindAddRole(mind, "OrbitraMindRoleRatvar");
                Assert.That(Server.System<RoleSystem>().MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role), Is.True);
                role!.Value.Comp2.Rule = rule;
                cult.Members.Add(mind);
            }
            Assert.That(system.TryActivateArk((ark, structure), user), Is.False, "Twenty-minute gate must hold.");
            cult.EarliestArk = TimeSpan.Zero;
            cult.ArkDefence = TimeSpan.Zero;
            Assert.That(system.TryActivateArk((ark, structure), user), Is.True);
            Assert.That(cult.Energy, Is.Zero);
            Assert.That(system.TryActivateArk((ark, structure), user), Is.False);
            if (destroy) SEntMan.DeleteEntity(ark);
            system.Update(0);
            Assert.That(cult.Won, Is.EqualTo(!destroy));
            Assert.That(cult.Lost, Is.EqualTo(destroy));
        });
    }

    [Test]
    public async Task ConversionPurificationBodyTransferAndSpending()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<OrbitraRatvarRuleSystem>();
            var ticker = Server.System<GameTicker>();
            Assert.That(ticker.StartGameRule(CultRule, out var rule), Is.True);
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            var minds = Server.System<MindSystem>();
            var roles = Server.System<RoleSystem>();
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var target = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var founder = minds.CreateMind(null);
            minds.TransferTo(founder, user);
            roles.MindAddRole(founder, "OrbitraMindRoleRatvar");
            Assert.That(roles.MindHasRole<OrbitraRatvarRoleComponent>(founder, out var role), Is.True);
            role!.Value.Comp2.Rule = rule;
            cult.Members.Add(founder);
            var targetMind = minds.CreateMind(null);
            minds.TransferTo(targetMind, target);
            var tablet = SEntMan.SpawnEntity("OrbitraRatvarTablet", map.GridCoords);
            var item = new Entity<OrbitraRatvarTabletComponent>(tablet, SEntMan.GetComponent<OrbitraRatvarTabletComponent>(tablet));
            Assert.That(Server.System<SharedHandsSystem>().TryPickup(user, tablet), Is.True);
            var sigil = SEntMan.SpawnEntity("OrbitraRatvarConversionSigil", map.GridCoords);
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(sigil).Rule = rule;
            Assert.That(SEntMan.GetComponent<TransformComponent>(sigil).Anchored, Is.True);
            Server.System<SharedTransformSystem>().SetCoordinates(target, SEntMan.GetComponent<TransformComponent>(sigil).Coordinates);
            Assert.That(system.CanConvert(item, user, target, out _), Is.True, "Conversion no longer requires cuffs.");
            Assert.That(system.TryConvert(item, user, target), Is.True);
            Assert.That(system.TryConvert(item, user, target), Is.False, "Already converted minds must not give progress.");
            Assert.That(cult.Converted.Count, Is.EqualTo(1));
            var replacement = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            minds.TransferTo(targetMind, replacement);
            Assert.That(system.TryGetCult(replacement, out _), Is.True);
            Assert.That(system.TryGetCult(target, out _), Is.False);
            Assert.That(system.TryPurify((rule, cult), targetMind), Is.True);
            minds.TransferTo(targetMind, target);
            Assert.That(system.CanConvert(item, user, target, out _), Is.False, "Purification grants immunity.");
            cult.ProtectedUntil.Clear();
            Server.System<MobStateSystem>().ChangeMobState(target, MobState.Dead);
            Assert.That(system.CanConvert(item, user, target, out _), Is.False);
            Server.System<MobStateSystem>().ChangeMobState(target, MobState.Critical);
            Assert.That(system.TryConvert(item, user, target), Is.True);
            Assert.That(cult.Converted.Count, Is.EqualTo(1), "Reconverting the same mind cannot unlock tiers.");
            cult.Energy = 30;
            Assert.That(system.TryCompleteScripture(item, user, "OrbitraRatvarBrass"), Is.True);
            Assert.That(cult.Energy, Is.Zero);
            Assert.That(system.TryCompleteScripture(item, user, "OrbitraRatvarBrass"), Is.False);
            Assert.That(system.TryCompleteScripture(item, target, "OrbitraRatvarBrass"), Is.False);
            cult.Generated = cult.TierThreeEnergy;
            while (cult.Converted.Count < cult.TierThreeConverts)
                cult.Converted.Add(SEntMan.SpawnEntity(null, MapCoordinates.Nullspace));
            cult.Energy = 10000;
            Assert.That(system.TryCompleteScripture(item, user, "OrbitraRatvarMarauder"), Is.True);
            Assert.That(system.TryCompleteScripture(item, user, "OrbitraRatvarMarauder"), Is.True);
            Assert.That(system.TryCompleteScripture(item, user, "OrbitraRatvarMarauder"), Is.False, "Two living shells reserve both marauder slots.");
        });
    }

    [Test]
    public async Task RatvarPrototypesAndProgression()
    {
        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var random = Server.ResolveDependency<IRobustRandom>();
            var selector = new OrbitraRatvarAntagCount();
            var antag = prototypes.Index(OrbitraRatvarCultistPrototype);
            Assert.That(antag.PrefRoles.Select(id => id.Id), Is.EquivalentTo(new[] { "OrbitraRatvarCultist" }));
            foreach (var preference in antag.PrefRoles)
                Assert.That(prototypes.Index(preference).SetPreference, Is.True,
                    "Hidden role labels are removed during preference validation and cannot opt a player into this mode.");
            Assert.That(selector.GetTargetAntagCount(random, 15), Is.EqualTo(2));
            Assert.That(selector.GetTargetAntagCount(random, 19), Is.EqualTo(2));
            Assert.That(selector.GetTargetAntagCount(random, 20), Is.EqualTo(3));
            Assert.That(selector.GetTargetAntagCount(random, 30), Is.EqualTo(3));
            var rule = SEntMan.SpawnEntity("OrbitraRatvarRule", Robust.Shared.Map.MapCoordinates.Nullspace);
            var component = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            var system = Server.System<OrbitraRatvarRuleSystem>();
            Assert.That(system.GetTier(component), Is.EqualTo(1));
            component.Generated = component.TierThreeEnergy;
            for (var i = 0; i < component.TierThreeConverts; i++)
                component.Converted.Add(SEntMan.SpawnEntity(null, Robust.Shared.Map.MapCoordinates.Nullspace));
            Assert.That(system.GetTier(component), Is.EqualTo(3));
            Assert.That(component.MaxMarauders, Is.EqualTo(2));
            Assert.That(component.ConversionDelay.TotalSeconds, Is.EqualTo(5));
            Assert.That(component.ArkDefence.TotalMinutes, Is.EqualTo(5));
            foreach (var scripture in prototypes.EnumeratePrototypes<OrbitraRatvarScripturePrototype>())
            {
                Assert.That(scripture.Energy, Is.GreaterThanOrEqualTo(0));
                Assert.That(scripture.Delay, Is.GreaterThan(TimeSpan.Zero));
                Assert.That(scripture.Tier, Is.InRange(1, 3));
                Assert.That(scripture.Repair || scripture.Result.HasValue, Is.True);
                if (scripture.Result is { } result) Assert.That(prototypes.HasIndex(result), Is.True);
            }
        });
    }
}
