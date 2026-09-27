using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Wires;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraRatvarAudioTest : GameTest
{
    private static readonly EntProtoId CultRule = "OrbitraRatvarRule";
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [Test]
    public async Task FinaleAndArkDestructionUseImportedSounds()
    {
        var map = await Pair.CreateTestMap();
        EntityUid ark = default;
        await Server.WaitPost(() =>
        {
            SEntMan.SpawnEntity("OrbitraRatvarFinale", map.GridCoords);
            ark = SEntMan.SpawnEntity("OrbitraRatvarArk", map.GridCoords);
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Blunt", 1500);
            Server.System<DamageableSystem>().TryChangeDamage(ark, damage, true);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(ark), Is.False);
            var sounds = SEntMan.EntityQuery<AudioComponent>().Select(audio => audio.FileName).ToArray();
            Assert.That(sounds.Count(path => path == "/Audio/_Orbitra/Ratvar/ratvar_reveal.ogg"), Is.EqualTo(1));
            Assert.That(sounds.Count(path => path == "/Audio/_Orbitra/Ratvar/ark_deathrattle.ogg"), Is.EqualTo(1));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task SuccessSoundRequiresCompletedOperation(bool cog, bool cancel)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid item = default;
        var started = false;
        var duplicate = false;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule(CultRule, out var rule);
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var minds = Server.System<MindSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, user);
            var roles = Server.System<RoleSystem>();
            roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
            roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
            role!.Value.Comp2.Rule = rule;
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            cult.Members.Add(mind);
            cult.Energy = 1000;
            item = SEntMan.SpawnEntity(cog ? "OrbitraRatvarIntegrationCog" : "OrbitraRatvarTablet", map.GridCoords);
            Server.System<SharedHandsSystem>().TryPickup(user, item);
            if (cog)
            {
                var apc = SEntMan.SpawnEntity("APCBasic", map.GridCoords);
                Server.System<SharedWiresSystem>().TogglePanel(apc, SEntMan.GetComponent<WiresPanelComponent>(apc), true);
                var system = Server.System<OrbitraRatvarIntegrationCogSystem>();
                var device = new Entity<OrbitraRatvarIntegrationCogComponent>(item, SEntMan.GetComponent<OrbitraRatvarIntegrationCogComponent>(item));
                started = system.TryStartInstall(device, user, apc);
            }
            else
            {
                var system = Server.System<OrbitraRatvarRuleSystem>();
                var tablet = new Entity<OrbitraRatvarTabletComponent>(item, SEntMan.GetComponent<OrbitraRatvarTabletComponent>(item));
                started = system.TryStartScripture(tablet, user, "OrbitraRatvarBrass");
                duplicate = system.TryStartScripture(tablet, user, "OrbitraRatvarBrass");
            }
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(duplicate, Is.False);
            Assert.That(Sounds(), Is.Zero, "Звук успеха не должен звучать в начале действия.");
        });
        await Pair.RunSeconds(0.5f);
        if (cancel)
            await Server.WaitPost(() => Server.System<SharedHandsSystem>().TryDrop(user, item));
        // Проверяем до конца короткого звука установки (0,44 секунды).
        await Pair.RunSeconds(cog ? 3.6f : 1.7f);
        await Server.WaitAssertion(() => Assert.That(Sounds(), Is.EqualTo(cancel ? 0 : 1)));

        int Sounds() => SEntMan.EntityQuery<AudioComponent>().Count(audio => audio.FileName ==
            (cog ? "/Audio/_Orbitra/Ratvar/integration_cog_install.ogg" : "/Audio/_Orbitra/Ratvar/invoke_general.ogg"));
    }
}
