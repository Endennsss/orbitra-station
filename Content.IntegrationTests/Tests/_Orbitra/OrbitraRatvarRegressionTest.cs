using System.Linq;
using Content.Client._Orbitra.UserInterface;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.Verbs.UI;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Server.Chat.Systems;
using Content.Server.Construction.Components;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Chat;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Content.Shared.Verbs;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraRatvarRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, NoLoadTestPrototypes = true };

    [TestCase("OrbitraRatvarGenerator")]
    [TestCase("OrbitraRatvarObelisk")]
    public async Task MechanismContextMenuStartsDeconstruction(string prototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid mechanism = default;
        EntityUid user = default;
        Verb? deconstruct = null;
        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            mechanism = SEntMan.SpawnEntity(prototype, map.GridCoords);
            var verbs = Server.System<Content.Server.Verbs.VerbSystem>()
                .GetLocalVerbs(mechanism, user, typeof(Verb));
            deconstruct = verbs.SingleOrDefault(verb => verb.Text == Loc.GetString("deconstructible-verb-begin-deconstruct"));
        });
        await Server.WaitAssertion(() => Assert.That(deconstruct, Is.Not.Null));
        await Server.WaitPost(() => deconstruct!.Act!());
        await Server.WaitAssertion(() =>
        {
            var construction = SEntMan.GetComponent<ConstructionComponent>(mechanism);
            Assert.That(construction.TargetNode, Is.EqualTo("dismantled"));
        });
        await Server.WaitPost(() => Server.System<Content.Server.Verbs.VerbSystem>()
            .GetLocalVerbs(mechanism, user, typeof(Verb)));
    }

    [Test]
    public async Task EveryStructureScriptureAnchorsOnce()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<OrbitraRatvarRuleSystem>();
            Server.System<GameTicker>().StartGameRule("OrbitraRatvarRule", out var rule);
            var cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationDataComponent>(station);
            Server.System<StationSystem>().AddGridToStation(station, map.Grid);
            cult.Station = station;
            cult.Generated = cult.TierThreeEnergy;
            for (var i = 0; i < cult.TierThreeConverts; i++)
                cult.Converted.Add(SEntMan.SpawnEntity(null, MapCoordinates.Nullspace));
            var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var minds = Server.System<MindSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, body);
            AddMember(mind, rule, cult);
            var tablet = SEntMan.SpawnEntity("OrbitraRatvarTablet", map.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickup(body, tablet), Is.True);
            var item = new Entity<OrbitraRatvarTabletComponent>(tablet, SEntMan.GetComponent<OrbitraRatvarTabletComponent>(tablet));

            foreach (var scripture in SProtoMan.EnumeratePrototypes<OrbitraRatvarScripturePrototype>().Where(s => s.Structure))
            {
                cult.Energy = cult.MaxEnergy;
                Assert.That(system.TryCompleteScripture(item, body, scripture.ID), Is.True, scripture.ID);
                var created = SEntMan.EntityQueryEnumerator<OrbitraRatvarStructureComponent, TransformComponent>();
                var count = 0;
                EntityUid result = default;
                while (created.MoveNext(out var uid, out var structure, out var transform))
                {
                    Assert.That(structure.Rule, Is.EqualTo(rule));
                    Assert.That(transform.Anchored, Is.True, scripture.ID);
                    result = uid;
                    count++;
                }
                Assert.That(count, Is.EqualTo(1), scripture.ID);
                Assert.That(cult.Energy, Is.EqualTo(cult.MaxEnergy - scripture.Energy));
                Assert.That(system.TryCompleteScripture(item, body, scripture.ID), Is.False, "Occupied location must reject another purchase.");
                SEntMan.DeleteEntity(result);
            }
            var gear = SProtoMan.Index<StartingGearPrototype>("OrbitraRatvarGear");
            Assert.That(gear.Storage.Values.SelectMany(v => v), Does.Not.Contain("OrbitraRatvarInstructions"));
        });
    }

    [Test]
    public async Task HiveChatIsPrivateAndDoesNotFallBackToSpeech()
    {
        var map = await Pair.CreateTestMap();
        EntityUid sender = default;
        EntityUid receiverMind = default;
        EntityUid senderMind = default;
        EntityUid rule = default;
        OrbitraRatvarRuleComponent cult = null!;
        await Server.WaitPost(() =>
        {
            Server.System<GameTicker>().StartGameRule("OrbitraRatvarRule", out rule);
            cult = SEntMan.GetComponent<OrbitraRatvarRuleComponent>(rule);
            var minds = Server.System<MindSystem>();
            sender = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            senderMind = minds.CreateMind(null);
            minds.TransferTo(senderMind, sender);
            AddMember(senderMind, rule, cult);
            var receiver = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            receiverMind = minds.CreateMind(ServerSession!.UserId);
            minds.TransferTo(receiverMind, receiver);
        });
        await Pair.RunTicksSync(5);
        var chat = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>();
        await Server.WaitPost(() => Server.System<ChatSystem>().TrySendInGameICMessage(sender, "+р ratvar-private-outsider", InGameICChatType.Speak, false));
        await Pair.RunTicksSync(5);
        await Client.WaitAssertion(() => Assert.That(chat.History.Any(m => m.Msg.Message.Contains("ratvar-private-outsider")), Is.False));

        await Server.WaitPost(() =>
        {
            AddMember(receiverMind, rule, cult);
            Server.System<RoleSystem>().MindHasRole<OrbitraRatvarRoleComponent>(senderMind, out var role);
            role!.Value.Comp2.NextMessage = TimeSpan.Zero;
            Server.System<ChatSystem>().TrySendInGameICMessage(sender, "+р ratvar-private-member [bold]safe[/bold]", InGameICChatType.Speak, false);
        });
        await Pair.RunTicksSync(5);
        await Client.WaitAssertion(() =>
        {
            var message = chat.History.Single(m => m.Msg.Message.Contains("ratvar-private-member")).Msg;
            Assert.That(message.Channel, Is.EqualTo(ChatChannel.Radio));
            Assert.That(message.WrappedMessage, Does.Contain("\\[bold]"));
        });
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<OrbitraRatvarRuleSystem>();
            Assert.That(system.CanSendHiveMessage(sender, "cooldown", out _, out _), Is.False);
        });
        await Server.WaitPost(() =>
        {
            Server.System<OrbitraRatvarRuleSystem>().TryPurify((rule, cult), senderMind);
            Server.System<ChatSystem>().TrySendInGameICMessage(sender, "+р ratvar-private-rejected", InGameICChatType.Speak, false);
        });
        await Pair.RunTicksSync(5);
        await Client.WaitAssertion(() => Assert.That(chat.History.Any(m => m.Msg.Message.Contains("ratvar-private-rejected")), Is.False));
    }

    [Test]
    public async Task VerbTooltipHasOnlyOrbitraFrame()
    {
        VerbMenuElement owner = null!;
        Control tip = null!;
        await Client.WaitPost(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            owner = new VerbMenuElement(new Verb { Text = "Ratvar", Message = "A long tooltip with [bold]formatting[/bold]." });
            ui.StateRoot.AddChild(owner);
            tip = owner.TooltipSupplier!(owner)!;
            ui.PopupRoot.AddChild(tip);
        });
        await Pair.RunTicksSync(2);
        await Client.WaitAssertion(() =>
        {
            Assert.That(tip.HasStyleClass("OrbitraTooltip"), Is.True);
            var native = (PanelContainer) tip.GetChild(0);
            Assert.That(native.HasStyleClass("OrbitraTooltipContent"), Is.True);
            Assert.That(native.TryGetStyleProperty<StyleBox>(PanelContainer.StylePropertyPanel, out var box), Is.True);
            Assert.That(box, Is.TypeOf<StyleBoxFlat>());
            Assert.That(((StyleBoxFlat) box!).BackgroundColor, Is.EqualTo(Color.Transparent));
        });
        await Client.WaitPost(() => owner.Dispose());
    }

    private void AddMember(EntityUid mind, EntityUid rule, OrbitraRatvarRuleComponent cult)
    {
        var roles = Server.System<RoleSystem>();
        roles.MindAddRole(mind, "OrbitraMindRoleRatvar");
        roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role);
        role!.Value.Comp2.Rule = rule;
        cult.Members.Add(mind);
    }
}
