using System;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.Administration.Managers;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Station.Components;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Connected admin command, per-cult isolation, real purchases and reversible participant overrides.</summary>
[TestFixture]
public sealed class OrbitraRatvarTestModeTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };
    private EntityUid _body;
    private EntityUid _station;
    private EntityCoordinates _origin;
    private OrbitraRatvarRuleSystem Cult => Server.System<OrbitraRatvarRuleSystem>();

    private void Command(string args) => Server.Resolve<IConsoleHost>().GetSessionShell(ServerSession!).ExecuteCommand("orbitra_ratvar_test " + args);

    private async Task Prepare()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            _origin = map.GridCoords;
            _station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationDataComponent>(_station);
            Server.System<StationSystem>().AddGridToStation(_station, map.Grid);
            _body = SEntMan.SpawnEntity("MobHuman", _origin);
            var minds = Server.System<MindSystem>();
            minds.TransferTo(minds.CreateMind(ServerSession!.UserId), _body);
            Server.Resolve<IAdminManager>().PromoteHost(ServerSession);
        });
        await Pair.RunTicksSync(2);
    }

    [Test]
    public async Task CommandGrantsOnceRejectsInvalidAndUnauthorizedChangesAndIsolatesCult()
    {
        await Prepare();
        await Server.WaitAssertion(() => Assert.That(Cult.TryGetCult(_body, out _), Is.False));
        await Server.WaitPost(() => Command("on"));
        Entity<OrbitraRatvarRuleComponent> rule = default;
        EntityUid other = default;
        await Server.WaitPost(() =>
        {
            Cult.TryGetCult(_body, out rule);
            Server.System<GameTicker>().StartGameRule("OrbitraRatvarRule", out other);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(rule.Comp.TestTier, Is.EqualTo(3));
            Assert.That(rule.Comp.Energy, Is.EqualTo(10000));
            Assert.That(rule.Comp.Members.Count, Is.EqualTo(1));
            Assert.That(SEntMan.Count<OrbitraRatvarTabletComponent>(), Is.EqualTo(1));
            Assert.That(SEntMan.GetComponent<OrbitraRatvarRuleComponent>(other).TestTier, Is.Null);
            Assert.That(Cult.CanConfigureTestMode(ServerSession!, 4, 10000, out _), Is.False);
            Assert.That(Cult.CanConfigureTestMode(ServerSession!, 3, -1, out _), Is.False);
            Assert.That(Cult.CanConfigureTestMode(ServerSession!, 3, 10001, out _), Is.False);
        });
        await Server.WaitPost(() => Command("on 2 500"));
        await Server.WaitAssertion(() =>
        {
            Assert.That(Cult.GetTier(rule.Comp), Is.EqualTo(2));
            Assert.That(rule.Comp.Energy, Is.EqualTo(500));
            Assert.That(SEntMan.Count<OrbitraRatvarTabletComponent>(), Is.EqualTo(1));
        });
        var changed = true;
        await Server.WaitPost(() =>
        {
            Server.Resolve<IAdminManager>().DeAdmin(ServerSession!);
            changed = Cult.TryConfigureTestMode(ServerSession!, 3, 10000);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(rule.Comp.TestTier, Is.EqualTo(2));
        });
        await Server.WaitPost(() =>
        {
            Server.Resolve<IAdminManager>().ReAdmin(ServerSession!);
            Command("off");
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(rule.Comp.TestTier, Is.Null);
            Assert.That(Cult.GetTier(rule.Comp), Is.EqualTo(1));
            Assert.That(rule.Comp.Energy, Is.EqualTo(500));
            Assert.That(rule.Comp.Converted, Is.Empty);
        });
    }

    [Test]
    public async Task SoloConstructionBindsAndChargesWhileArkStillRequiresValidPlacement()
    {
        await Prepare();
        Entity<OrbitraRatvarRuleComponent> rule = default;
        Entity<OrbitraRatvarTabletComponent> tablet = default;
        Entity<OrbitraRatvarStructureComponent> ark = default;
        var built = false;
        await Server.WaitPost(() =>
        {
            Command("on");
            Cult.TryGetCult(_body, out rule);
            var item = SEntMan.EntityQuery<OrbitraRatvarTabletComponent>().Single();
            tablet = (item.Owner, item);
            Server.System<SharedHandsSystem>().TryPickupAnyHand(_body, tablet);
            built = Cult.TryCompleteScripture(tablet, _body, "OrbitraRatvarTransmissionSigil");
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(built, Is.True);
            Assert.That(rule.Comp.Energy, Is.EqualTo(9900));
            Assert.That(SEntMan.EntityQuery<OrbitraRatvarTransmissionComponent>().Count(), Is.EqualTo(1));
            Assert.That(rule.Comp.TransmissionSigils.Count, Is.EqualTo(1));
        });
        await Server.WaitPost(() =>
        {
            var uid = SEntMan.SpawnEntity("OrbitraRatvarArk", _origin);
            ark = (uid, SEntMan.GetComponent<OrbitraRatvarStructureComponent>(uid));
            ark.Comp.Rule = rule;
            rule.Comp.Ark = uid;
        });
        await Server.WaitAssertion(() => Assert.That(Cult.CanActivateArk(ark, _body, out _), Is.True));
        await Server.WaitPost(() => rule.Comp.Station = null);
        await Server.WaitAssertion(() => Assert.That(Cult.CanActivateArk(ark, _body, out _), Is.False));
        await Server.WaitPost(() =>
        {
            rule.Comp.Station = _station;
            Command("off");
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(Cult.CanActivateArk(ark, _body, out _), Is.False);
            Assert.That(rule.Comp.EarliestArk, Is.EqualTo(TimeSpan.FromMinutes(20)));
            Assert.That(rule.Comp.ArkCultists, Is.EqualTo(3));
            Assert.That(rule.Comp.MaxMarauders, Is.EqualTo(2));
        });
    }
}
