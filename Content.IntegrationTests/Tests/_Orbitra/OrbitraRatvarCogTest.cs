using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Wires;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Exercises timed installation and authoritative, cult-isolated battery extraction.</summary>
[TestFixture]
public sealed class OrbitraRatvarCogTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [TestCase("normal", true)]
    [TestCase("drop", false)]
    [TestCase("mind", false)]
    [TestCase("closed", false)]
    [TestCase("mind-return", false)]
    [TestCase("apc-delete", false)]
    [TestCase("cult-end", false)]
    public async Task InstallationRevalidatesAuthority(string scenario, bool expected)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid apc = default;
        EntityUid cog = default;
        EntityUid rule = default;
        EntityUid mind = default;
        var started = false;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule("OrbitraRatvarRule", out rule);
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            apc = SEntMan.SpawnEntity("APCBasic", map.GridCoords);
            cog = SEntMan.SpawnEntity("OrbitraRatvarIntegrationCog", map.GridCoords);
            var minds = Server.System<MindSystem>();
            mind = minds.CreateMind(null);
            minds.TransferTo(mind, user);
            var roles = Server.System<RoleSystem>();
            roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
            roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
            role!.Value.Comp2.Rule = rule;
            SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Members.Add(mind);
            Server.System<SharedWiresSystem>().TogglePanel(apc, SEntMan.GetComponent<WiresPanelComponent>(apc), true);
            Server.System<SharedHandsSystem>().TryPickup(user, cog);
            started = Server.System<OrbitraRatvarIntegrationCogSystem>().TryStartInstall(
                (cog, SEntMan.GetComponent<OrbitraRatvarIntegrationCogComponent>(cog)), user, apc);
        });
        await Server.WaitAssertion(() => Assert.That(started, Is.True));
        await Pair.RunSeconds(1);
        await Server.WaitPost(() =>
        {
            switch (scenario)
            {
                case "drop": Server.System<SharedHandsSystem>().TryDrop(user, cog); break;
                case "mind":
                case "mind-return": Server.System<MindSystem>().TransferTo(mind, null); break;
                case "closed": Server.System<SharedWiresSystem>().TogglePanel(apc, SEntMan.GetComponent<WiresPanelComponent>(apc), false); break;
                case "apc-delete": SEntMan.DeleteEntity(apc); break;
                case "cult-end": Server.System<GameTicker>().EndGameRule(rule); break;
            }
        });
        if (scenario == "mind-return")
        {
            await Pair.RunTicksSync(2);
            await Server.WaitPost(() => Server.System<MindSystem>().TransferTo(mind, user));
        }
        await Pair.RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<OrbitraRatvarInstalledCogComponent>(apc), Is.EqualTo(expected));
            if (expected)
            {
                Assert.That(SEntMan.GetComponent<OrbitraRatvarInstalledCogComponent>(apc).Rule, Is.EqualTo(rule));
                Assert.That(SEntMan.GetComponent<WiresPanelComponent>(apc).Open, Is.False);
            }
        });
    }

    [TestCase(50000f, false, 20)]
    [TestCase(24000f, false, 0)]
    [TestCase(50000f, true, 0)]
    public async Task ExtractionDebitsBatteryAndOnlyCreditsOwner(float charge, bool full, int expected)
    {
        var map = await Pair.CreateTestMap();
        float remaining = 0;
        OrbitraRatvarRuleComponent cult = default!;
        OrbitraRatvarRuleComponent other = default!;
        await Server.WaitPost(() =>
        {
            var ticker = Server.System<GameTicker>();
            ticker.StartGameRule("OrbitraRatvarRule", out var rule);
            ticker.StartGameRule("OrbitraRatvarRule", out var second);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            other = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(second);
            cult.Energy = full ? cult.MaxEnergy : 0;
            var apc = SEntMan.SpawnEntity("APCBasic", map.GridCoords);
            var cog = SEntMan.SpawnEntity("OrbitraRatvarIntegrationCog", map.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            containers.Insert(cog, containers.EnsureContainer<ContainerSlot>(apc, "orbitra-ratvar-cog"));
            var installed = SEntMan.AddComponent<OrbitraRatvarInstalledCogComponent>(apc);
            installed.Rule = rule;
            var batteries = Server.System<SharedBatterySystem>();
            batteries.SetCharge((apc, SEntMan.GetComponent<BatteryComponent>(apc)), charge);
            Server.System<OrbitraRatvarIntegrationCogSystem>().TryExtract((apc, installed));
            remaining = batteries.GetCharge(apc).Charge;
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(remaining, Is.EqualTo(charge - expected * 100));
            Assert.That(cult.Energy, Is.EqualTo(full ? cult.MaxEnergy : expected));
            Assert.That(cult.Generated, Is.EqualTo(expected));
            Assert.That(other.Generated, Is.Zero);
            Assert.That(other.Energy, Is.EqualTo(other.StartingEnergy));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CrewCanDestroyInstalledCogWithoutDestroyingApc(bool deleteApc)
    {
        var map = await Pair.CreateTestMap();
        EntityUid apc = default;
        EntityUid cog = default;
        EntityUid spare = default;
        var handled = false;
        await Server.WaitPost(() =>
        {
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var crowbar = SEntMan.SpawnEntity("Crowbar", map.GridCoords);
            Server.System<SharedHandsSystem>().TryPickup(user, crowbar);
            apc = SEntMan.SpawnEntity("APCBasic", map.GridCoords);
            cog = SEntMan.SpawnEntity("OrbitraRatvarIntegrationCog", map.GridCoords);
            spare = SEntMan.SpawnEntity("SheetSteel1", map.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            containers.Insert(cog, containers.EnsureContainer<ContainerSlot>(apc, "orbitra-ratvar-cog"));
            containers.Insert(spare, containers.EnsureContainer<ContainerSlot>(apc, "orbitra-test-spare"));
            var installed = SEntMan.AddComponent<OrbitraRatvarInstalledCogComponent>(apc);
            installed.NextExtraction = TimeSpan.MaxValue;
            Server.System<SharedWiresSystem>().TogglePanel(apc, SEntMan.GetComponent<WiresPanelComponent>(apc), true);
            var interaction = new InteractUsingEvent(user, crowbar, apc, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(apc, interaction);
            handled = interaction.Handled;
        });
        await Server.WaitAssertion(() => Assert.That(handled, Is.True));
        if (deleteApc)
        {
            await Pair.RunSeconds(1);
            await Server.WaitPost(() => SEntMan.DeleteEntity(apc));
            await Pair.RunSeconds(6);
            await Server.WaitAssertion(() => Assert.That(SEntMan.EntityExists(apc), Is.False));
            return;
        }
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(cog), Is.False);
            Assert.That(SEntMan.EntityExists(apc), Is.True);
            Assert.That(SEntMan.EntityExists(spare), Is.True);
            Assert.That(SEntMan.HasComponent<BatteryComponent>(apc), Is.True);
            Assert.That(SEntMan.HasComponent<OrbitraRatvarInstalledCogComponent>(apc), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ExtractionCannotBypassIntervalOrInstalledSettings(bool foreignSettings)
    {
        var map = await Pair.CreateTestMap();
        OrbitraRatvarRuleComponent cult = default!;
        float remaining = 0;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule("OrbitraRatvarRule", out var rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Energy = 0;
            var apc = SEntMan.SpawnEntity("APCBasic", map.GridCoords);
            var cog = SEntMan.SpawnEntity("OrbitraRatvarIntegrationCog", map.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            containers.Insert(cog, containers.EnsureContainer<ContainerSlot>(apc, "orbitra-ratvar-cog"));
            var installed = SEntMan.AddComponent<OrbitraRatvarInstalledCogComponent>(apc);
            installed.Rule = rule;
            var system = Server.System<OrbitraRatvarIntegrationCogSystem>();
            var settings = SEntMan.GetComponent<OrbitraRatvarIntegrationCogComponent>(cog);
            if (foreignSettings)
            {
                var other = SEntMan.SpawnEntity("OrbitraRatvarIntegrationCog", map.GridCoords);
                settings = SEntMan.GetComponent<OrbitraRatvarIntegrationCogComponent>(other);
                settings.EnergyPerInterval = 200;
            }
            system.TryExtract((apc, installed));
            if (!foreignSettings)
                system.TryExtract((apc, installed));
            remaining = Server.System<SharedBatterySystem>().GetCharge(apc).Charge;
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(cult.Generated, Is.EqualTo(20));
            Assert.That(cult.Energy, Is.EqualTo(20));
            Assert.That(remaining, Is.EqualTo(48000));
        });
    }

    [Test]
    public async Task ClosedApcRequiresOpeningBeforeInstallation()
    {
        var map = await Pair.CreateTestMap();
        EntityUid apc = default;
        EntityUid cog = default;
        EntityUid user = default;
        await Server.WaitPost(() =>
        {
            user = CreateCultist(map.GridCoords, out _);
            apc = SEntMan.SpawnEntity("APCBasic", map.GridCoords);
            cog = SEntMan.SpawnEntity("OrbitraRatvarIntegrationCog", map.GridCoords);
            Server.System<SharedHandsSystem>().TryPickup(user, cog);
            SEntMan.EventBus.RaiseLocalEvent(cog, new AfterInteractEvent(user, cog, apc, map.GridCoords, true));
        });
        await Pair.RunSeconds(4);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<WiresPanelComponent>(apc).Open, Is.False));
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<WiresPanelComponent>(apc).Open, Is.True);
            Assert.That(Server.System<SharedHandsSystem>().IsHolding(user, cog), Is.True);
            Assert.That(SEntMan.HasComponent<OrbitraRatvarInstalledCogComponent>(apc), Is.False);
        });
        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(cog,
            new AfterInteractEvent(user, cog, apc, map.GridCoords, true)));
        await Pair.RunSeconds(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<OrbitraRatvarInstalledCogComponent>(apc), Is.True);
            Assert.That(Server.System<SharedHandsSystem>().IsHolding(user, cog), Is.False);
        });
    }

    [Test]
    public async Task TwoCultsCannotInstallInSameApcOrStealOwnership()
    {
        var map = await Pair.CreateTestMap();
        var users = new EntityUid[2];
        var cogs = new EntityUid[2];
        var rules = new EntityUid[2];
        var started = new bool[2];
        EntityUid apc = default;
        await Server.WaitPost(() =>
        {
            apc = SEntMan.SpawnEntity("APCBasic", map.GridCoords);
            Server.System<SharedWiresSystem>().TogglePanel(apc, SEntMan.GetComponent<WiresPanelComponent>(apc), true);
            for (var i = 0; i < 2; i++)
            {
                users[i] = CreateCultist(map.GridCoords, out rules[i]);
                cogs[i] = SEntMan.SpawnEntity("OrbitraRatvarIntegrationCog", map.GridCoords);
                Server.System<SharedHandsSystem>().TryPickup(users[i], cogs[i]);
                started[i] = Server.System<OrbitraRatvarIntegrationCogSystem>().TryStartInstall(
                    (cogs[i], SEntMan.GetComponent<OrbitraRatvarIntegrationCogComponent>(cogs[i])), users[i], apc);
            }
        });
        await Server.WaitAssertion(() => Assert.That(started, Is.All.True));
        await Pair.RunSeconds(8);
        await Server.WaitAssertion(() =>
        {
            var containers = Server.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainer(apc, "orbitra-ratvar-cog", out var slot), Is.True);
            Assert.That(slot!.ContainedEntities, Has.Count.EqualTo(1));
            var winner = slot.ContainedEntities[0] == cogs[0] ? 0 : 1;
            Assert.That(SEntMan.GetComponent<OrbitraRatvarInstalledCogComponent>(apc).Rule, Is.EqualTo(rules[winner]));
            Assert.That(Server.System<SharedHandsSystem>().IsHolding(users[1 - winner], cogs[1 - winner]), Is.True);
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rules[winner]).Generated, Is.GreaterThan(0));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rules[1 - winner]).Generated, Is.Zero);
            Assert.That(Server.System<SharedBatterySystem>().GetCharge(apc).Charge, Is.LessThan(50000));
        });
    }

    private EntityUid CreateCultist(Robust.Shared.Map.EntityCoordinates coordinates, out EntityUid rule)
    {
        Server.System<GameTicker>().StartGameRule("OrbitraRatvarRule", out rule);
        var user = SEntMan.SpawnEntity("MobHuman", coordinates);
        var minds = Server.System<MindSystem>();
        var mind = minds.CreateMind(null);
        minds.TransferTo(mind, user);
        var roles = Server.System<RoleSystem>();
        roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
        roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
        role!.Value.Comp2.Rule = rule;
        SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule).Members.Add(mind);
        return user;
    }

    [TestCase("cog")]
    [TestCase("apc")]
    [TestCase("battery")]
    [TestCase("cult")]
    public async Task ExtractionStopsWhenInstallationBecomesInvalid(string removed)
    {
        var map = await Pair.CreateTestMap();
        EntityUid rule = default;
        EntityUid apc = default;
        EntityUid cog = default;
        OrbitraRatvarRuleComponent cult = default!;
        var generated = 0;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule("OrbitraRatvarRule", out rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            apc = SEntMan.SpawnEntity("APCBasic", map.GridCoords);
            cog = SEntMan.SpawnEntity("OrbitraRatvarIntegrationCog", map.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            containers.Insert(cog, containers.EnsureContainer<ContainerSlot>(apc, "orbitra-ratvar-cog"));
            SEntMan.AddComponent<OrbitraRatvarInstalledCogComponent>(apc).Rule = rule;
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() => Assert.That(cult.Generated, Is.GreaterThan(0)));
        await Server.WaitPost(() =>
        {
            generated = cult.Generated;
            switch (removed)
            {
                case "cog": SEntMan.DeleteEntity(cog); break;
                case "apc": SEntMan.DeleteEntity(apc); break;
                case "battery": SEntMan.RemoveComponent<BatteryComponent>(apc); break;
                case "cult": Server.System<GameTicker>().EndGameRule(rule); break;
            }
        });
        await Pair.RunSeconds(4);
        await Server.WaitAssertion(() => Assert.That(cult.Generated, Is.EqualTo(generated)));
    }
}
