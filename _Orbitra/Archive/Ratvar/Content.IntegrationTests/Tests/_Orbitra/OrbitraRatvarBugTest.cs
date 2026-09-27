using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.NPC.Components;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraRatvarBugTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [Test]
    public async Task ArkUsesItsOwnDestructionThreshold()
    {
        var map = await Pair.CreateTestMap();
        EntityUid ark = default;
        await Server.WaitPost(() =>
        {
            ark = SEntMan.SpawnEntity("OrbitraRatvarArk", map.GridCoords);
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Blunt", 1199);
            Server.System<DamageableSystem>().TryChangeDamage(ark, damage, true);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.EntityExists(ark), Is.True,
            "Ковчег не должен наследовать разрушение обычного механизма при 175 урона."));
        await Server.WaitPost(() =>
        {
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Blunt", 1);
            Server.System<DamageableSystem>().TryChangeDamage(ark, damage, true);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.EntityExists(ark), Is.False));
    }

    [Test]
    public async Task QueuedArkCannotWinOnTheSameTick()
    {
        var map = await Pair.CreateTestMap();
        Entity<OrbitraRatvarRuleComponent> cult = default;
        await Server.WaitPost(() =>
        {
            cult = CreateCult();
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationDataComponent>(station);
            Server.System<StationSystem>().AddGridToStation(station, map.Grid);
            cult.Comp.Station = station;
            var ark = SEntMan.SpawnEntity("OrbitraRatvarArk", map.GridCoords);
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(ark).Rule = cult.Owner;
            cult.Comp.Ark = ark;
            cult.Comp.SummonAt = TimeSpan.Zero;
            SEntMan.QueueDeleteEntity(ark);
            Server.System<OrbitraRatvarRuleSystem>().Update(0);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(cult.Comp.Won, Is.False);
            Assert.That(cult.Comp.Lost, Is.True);
            Assert.That(cult.Comp.FinishAt, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ScriptureCancelsWhenCultOrMindChanges(bool changeMind)
    {
        var map = await Pair.CreateTestMap();
        Entity<OrbitraRatvarRuleComponent> cult = default;
        Entity<OrbitraRatvarRuleComponent> otherCult = default;
        Entity<OrbitraRatvarTabletComponent> tablet = default;
        EntityUid user = default;
        EntityUid mind = default;
        var started = false;
        await Server.WaitPost(() =>
        {
            cult = CreateCult();
            otherCult = CreateCult();
            (user, mind) = CreateMember(cult, map.GridCoords);
            var uid = SEntMan.SpawnEntity("OrbitraRatvarTablet", map.GridCoords);
            tablet = (uid, SEntMan.GetComponent<OrbitraRatvarTabletComponent>(uid));
            Server.System<SharedHandsSystem>().TryPickup(user, uid);
            started = Server.System<OrbitraRatvarRuleSystem>().TryStartScripture(tablet, user, "OrbitraRatvarBrass");
        });
        await Server.WaitAssertion(() => Assert.That(started, Is.True));
        await Pair.RunSeconds(0.25f);
        await Server.WaitPost(() =>
        {
            if (changeMind)
                Server.System<MindSystem>().TransferTo(mind, null);
            else
            {
                Server.System<RoleSystem>().MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
                role!.Value.Comp2.Rule = otherCult.Owner;
                otherCult.Comp.Members.Add(mind);
            }
        });
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() => Assert.That(tablet.Comp.Busy, Is.False,
            "Любое писание должно прерваться при потере исходного сознания или культа, а не только целевое заклинание."));
        if (changeMind)
            await Server.WaitPost(() => Server.System<MindSystem>().TransferTo(mind, user));
        await Pair.RunSeconds(2.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(cult.Comp.Energy, Is.EqualTo(1000));
            Assert.That(otherCult.Comp.Energy, Is.EqualTo(1000));
            Assert.That(SEntMan.EntityQuery<MetaDataComponent>().Any(m => m.EntityPrototype?.ID == "SheetBrass10"), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task MembershipRevocationClearsBodyFaction(bool loseArk)
    {
        var map = await Pair.CreateTestMap();
        Entity<OrbitraRatvarRuleComponent> cult = default;
        EntityUid user = default;
        EntityUid mind = default;
        await Server.WaitPost(() =>
        {
            cult = CreateCult();
            (user, mind) = CreateMember(cult, map.GridCoords);
            Server.System<OrbitraRatvarRuleSystem>().Update(0);
        });
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<OrbitraRatvarBodyComponent>(user), Is.True));
        await Server.WaitPost(() =>
        {
            if (loseArk)
            {
                cult.Comp.SummonAt = TimeSpan.Zero;
                cult.Comp.Ark = null;
                Server.System<OrbitraRatvarRuleSystem>().Update(0);
            }
            else
                Server.System<RoleSystem>().MindRemoveRole<OrbitraRatvarRoleComponent>(mind);
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<OrbitraRatvarBodyComponent>(user), Is.False);
            Assert.That(SEntMan.GetComponent<NpcFactionMemberComponent>(user).Factions.Any(f => f.Id == "OrbitraRatvar"), Is.False);
        });
    }

    private Entity<OrbitraRatvarRuleComponent> CreateCult()
    {
        Server.System<GameTicker>().StartGameRule(CultRule, out var uid);
        var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(uid);
        cult.Energy = 1000;
        return (uid, cult);
    }

    [Test]
    public async Task SubmissionRecoversAfterTargetDeletion()
    {
        var map = await Pair.CreateTestMap();
        Entity<OrbitraRatvarSubmissionComponent> sigil = default;
        EntityUid target = default;
        EntityCoordinates center = default;
        var started = false;
        await Server.WaitPost(() =>
        {
            var cult = CreateCult();
            var uid = SEntMan.SpawnEntity("OrbitraRatvarConversionSigil", map.GridCoords);
            sigil = (uid, SEntMan.GetComponent<OrbitraRatvarSubmissionComponent>(uid));
            SEntMan.GetComponent<OrbitraRatvarStructureComponent>(uid).Rule = cult.Owner;
            center = SEntMan.GetComponent<TransformComponent>(uid).Coordinates;
            CreateMember(cult, center.Offset(new Vector2(-1, 0)));
            target = SEntMan.SpawnEntity("MobHuman", center);
            var minds = Server.System<MindSystem>();
            minds.TransferTo(minds.CreateMind(null), target);
            started = Server.System<OrbitraRatvarRuleSystem>().TryStartSubmission(sigil, target);
        });
        await Server.WaitAssertion(() => Assert.That(started, Is.True));
        await Pair.RunSeconds(0.5f);
        await Server.WaitPost(() => SEntMan.DeleteEntity(target));
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() => Assert.That(sigil.Comp.Target, Is.Null,
            "Удаление цели не должно навсегда занимать печать."));
        await Server.WaitPost(() =>
        {
            target = SEntMan.SpawnEntity("MobHuman", center);
            var minds = Server.System<MindSystem>();
            minds.TransferTo(minds.CreateMind(null), target);
        });
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() => Assert.That(Server.System<OrbitraRatvarRuleSystem>().TryGetCult(target, out _), Is.True));
    }

    private (EntityUid Body, EntityUid Mind) CreateMember(Entity<OrbitraRatvarRuleComponent> cult, EntityCoordinates coordinates)
    {
        var body = SEntMan.SpawnEntity("MobHuman", coordinates);
        var minds = Server.System<MindSystem>();
        var mind = minds.CreateMind(null);
        minds.TransferTo(mind, body);
        var roles = Server.System<RoleSystem>();
        roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
        roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
        role!.Value.Comp2.Rule = cult.Owner;
        cult.Comp.Members.Add(mind);
        return (body, mind);
    }
}
