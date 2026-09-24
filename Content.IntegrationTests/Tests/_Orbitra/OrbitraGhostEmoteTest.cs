using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._Orbitra.Stylesheets;
using Content.Client._Orbitra.UserInterface;
using Content.Client.UserInterface.Systems.Ghost.Controls;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chat.Systems;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Shared.Ghost.Systems;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Emoting;
using Content.Shared.Roles;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Speech;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraGhostEmoteTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true, NoLoadTestPrototypes = true };

    private static System.Collections.Generic.IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private async Task Click(BaseButton button)
    {
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
            await Client.DoGuiEvent(button, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, state,
                new ScreenCoordinates(button.GlobalPixelPosition + button.PixelSize / 2, button.Window!.Id),
                false, button.Size / 2, button.PixelSize / 2));
    }

    [Test]
    public async Task GhostCardsWrapWithoutOverflowAndKeepTargetsAfterSearch()
    {
        GhostTargetWindow window = null!;
        OrbitraGhostCardGrid grid = null!;
        Control[] cards = null!;
        var warps = Enumerable.Range(0, 8).Select(i => new GhostWarp(new NetEntity(91000 + i),
            $"Place {i}: long destination name that needs to wrap inside its card", true)).ToArray();
        await Client.WaitPost(() =>
        {
            window = new GhostTargetWindow();
            window.OpenCentered();
            window.UpdateWarps(warps);
            grid = Descendants(window).OfType<OrbitraGhostCardGrid>().Single();
            cards = grid.Children.ToArray();
        });
        try
        {
            await Pair.RunTicksSync(3);
            foreach (var (width, columns) in new[] { (900f, 4), (640f, 3), (328f, 1), (900f, 4) })
            {
                await Client.WaitPost(() =>
                {
                    grid.Measure(new Vector2(width, float.PositiveInfinity));
                    grid.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(width, grid.DesiredSize.Y)));
                });
                await Client.WaitAssertion(() =>
                {
                    Assert.That(grid.Columns, Is.EqualTo(columns));
                    Assert.That(grid.DesiredSize.X, Is.LessThanOrEqualTo(width + 1));
                    foreach (var card in cards)
                    {
                        Assert.That(card.Size.Y, Is.GreaterThanOrEqualTo(44));
                        Assert.That(card.Position.X + card.Size.X, Is.LessThanOrEqualTo(width + 1));
                    }
                    Assert.That(cards[columns].Position.Y, Is.GreaterThan(cards[0].Position.Y));
                    if (columns > 1)
                    {
                        Assert.That(cards[1].Position.X, Is.GreaterThan(cards[0].Position.X));
                        Assert.That(cards[1].Position.Y, Is.EqualTo(cards[0].Position.Y));
                    }
                });
            }
            await Client.WaitPost(() =>
            {
                window.FindControl<LineEdit>("SearchBar").SetText("Place 3:", true);
                grid.Measure(new Vector2(900, float.PositiveInfinity));
                grid.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(900, grid.DesiredSize.Y)));
            });
            await Client.WaitAssertion(() =>
            {
                Assert.That(grid.Children.Count(c => c.Visible), Is.EqualTo(1));
                Assert.That(cards[3].Position.X, Is.EqualTo(0));
            });
            await Client.WaitPost(() =>
            {
                window.FindControl<LineEdit>("SearchBar").SetText("", true);
                window.UpdateWarps(warps);
            });
            await Client.WaitAssertion(() => Assert.That(grid.Children.ToArray(), Is.EqualTo(cards)));
        }
        finally
        {
            await Client.WaitPost(() => window.Dispose());
        }
    }

    [Test]
    public async Task GhostSectionsKeepRowsSearchAndDepartmentAccents()
    {
        GhostTargetWindow window = null!;
        ContainerButton antagonist = null!;
        ContainerButton stationAntagonist = null!;
        var warps = new[]
        {
            new GhostWarp(new NetEntity(90001), "Security", false) { CharacterName = "Security", Job = "SecurityOfficer" },
            new GhostWarp(new NetEntity(90002), "Engineering", false) { CharacterName = "Engineering", Job = "StationEngineer" },
            new GhostWarp(new NetEntity(90003), "Cargo", false) { CharacterName = "Cargo", Job = "CargoTechnician" },
            new GhostWarp(new NetEntity(90004), "Command", false) { CharacterName = "Command", Job = "Captain" },
            new GhostWarp(new NetEntity(90005), "Medical", false) { CharacterName = "Medical", Job = "MedicalDoctor" },
            new GhostWarp(new NetEntity(90006), "Antagonist", false) { CharacterName = "Antagonist", Job = "Passenger", IsAntagonist = true },
        };
        await Client.WaitPost(() =>
        {
            window = new GhostTargetWindow();
            window.OpenCentered();
            window.UpdateWarps(warps);
            antagonist = Descendants(window).OfType<ContainerButton>().Single(b =>
                b.ToolTip?.StartsWith("Antagonist\n") == true && b.HasStyleClass("OrbitraGhostDepartmentantagonists"));
            stationAntagonist = Descendants(window).OfType<ContainerButton>().Single(b =>
                b.ToolTip?.StartsWith("Antagonist\n") == true && !b.HasStyleClass("OrbitraGhostDepartmentantagonists"));
        });
        try
        {
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() =>
            {
                Assert.That(antagonist.VisibleInTree, Is.False);
                Assert.That(stationAntagonist.VisibleInTree, Is.True);
                foreach (var (department, accent) in OrbitraGhostPalette.Departments)
                {
                    var header = Descendants(window).OfType<Button>().Single(b => b.HasStyleClass("OrbitraGhostDepartment" + department));
                    Assert.That(header.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var box), Is.True);
                    Assert.That(box, Is.TypeOf<StyleBoxFlat>());
                    Assert.That(((StyleBoxFlat) box).BorderColor, Is.EqualTo(accent));
                    var card = Descendants(window).OfType<ContainerButton>().Single(b =>
                        b.HasStyleClass("OrbitraGhostCard") && b.HasStyleClass("OrbitraGhostDepartment" + department));
                    Assert.That(card.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var cardBox), Is.True);
                    Assert.That(((StyleBoxFlat) cardBox).BackgroundColor,
                        Is.EqualTo(OrbitraGhostPalette.CardBackground(accent, 0.16f)));
                }
            });
            var tabs = Descendants(window).OfType<Button>().Where(b => b.HasStyleClass("OrbitraJournalTab") && b.ToggleMode).ToArray();
            await Click(tabs[1]);
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                Assert.That(antagonist.VisibleInTree, Is.True);
                Assert.That(stationAntagonist.VisibleInTree, Is.False);
                Assert.That(tabs[0].Pressed, Is.False);
                Assert.That(tabs[1].Pressed, Is.True);
            });
            await Client.WaitPost(() =>
            {
                window.FindControl<LineEdit>("SearchBar").SetText("Antagonist", true);
                window.UpdateWarps(warps);
            });
            await Client.WaitAssertion(() =>
            {
                Assert.That(antagonist.VisibleInTree, Is.True);
                Assert.That(Descendants(window).OfType<ContainerButton>().Single(b =>
                    b.ToolTip?.StartsWith("Antagonist\n") == true && b.HasStyleClass("OrbitraGhostDepartmentantagonists")), Is.SameAs(antagonist));
            });
            await Click(tabs[0]);
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                Assert.That(stationAntagonist.VisibleInTree, Is.True);
                Assert.That(antagonist.VisibleInTree, Is.False);
            });
            await Client.WaitPost(() =>
            {
                warps[5] = new GhostWarp(new NetEntity(90006), "Antagonist", false)
                    { CharacterName = "Antagonist", Job = "Passenger", IsAntagonist = false };
                window.UpdateWarps(warps);
            });
            await Client.WaitAssertion(() =>
            {
                Assert.That(antagonist.Disposed, Is.True);
                Assert.That(Descendants(window).OfType<ContainerButton>().Single(b =>
                    b.ToolTip?.StartsWith("Antagonist\n") == true), Is.SameAs(stationAntagonist));
                Assert.That(stationAntagonist.VisibleInTree, Is.True);
            });
            foreach (var size in new[] { new Vector2(360, 280), new Vector2(600, 440) })
                await Client.WaitPost(() =>
                {
                    window.Measure(size);
                    window.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                });
        }
        finally
        {
            await Client.WaitPost(() => window.Dispose());
        }
    }

    [Test]
    public async Task EmoteCooldownLastsTwoSecondsAndRejectedAttemptsDoNotExtendIt()
    {
        bool first = false, second = true, expired = false;
        int afterMenu = 0, afterBlockedInputs = 0, afterAutomatic = 0, afterText = 0;
        TimeSpan duration = default, deadline = default, afterRejection = default;
        await Server.WaitPost(() =>
        {
            var chat = SEntMan.System<ChatSystem>();
            var session = ServerSession!;
            var target = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<SpeechComponent>(target).AllowedEmotes.Add("Sigh");
            SEntMan.AddComponent<EmotingComponent>(target);
            var oldEntity = session.AttachedEntity;
            var minds = SEntMan.System<MindSystem>();
            var mind = minds.CreateMind(session.UserId, "Emote test");
            minds.TransferTo(mind, target);
            Server.PlayerMan.SetAttachedEntity(session, target);
            var recorder = SEntMan.System<OrbitraEmoteTestSystem>();
            recorder.Target = SEntMan.GetNetEntity(target);
            recorder.Count = 0;
            var menu = SEntMan.System<EmotesMenuSystem>();
            var play = typeof(EmotesMenuSystem).GetMethod("OnPlayEmote", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var message = new PlayEmoteMessage("Sigh");
            var args = new EntitySessionEventArgs(session);
            var trigger = SProtoMan.Index<EmotePrototype>(message.ProtoId).ChatTriggers.First();
            var timers = (System.Collections.Generic.Dictionary<ICommonSession, TimeSpan>) typeof(ChatSystem)
                .GetField("_orbitraNextEmotes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chat)!;
            try
            {
                play.Invoke(menu, new object[] { message, args });
                afterMenu = recorder.Count;
                chat.TrySendInGameICMessage(target, trigger, InGameICChatType.Emote, false, player: session);
                play.Invoke(menu, new object[] { message, args });
                afterBlockedInputs = recorder.Count;
                chat.TryEmoteWithChat(target, message.ProtoId);
                afterAutomatic = recorder.Count;
                timers[session] = SGameTiming.RealTime;
                chat.TrySendInGameICMessage(target, trigger, InGameICChatType.Emote, false, player: session);
                play.Invoke(menu, new object[] { message, args });
                afterText = recorder.Count;
            }
            finally
            {
                recorder.Target = null;
                minds.SetUserId(mind, null);
                Server.PlayerMan.SetAttachedEntity(session, oldEntity);
                SEntMan.DeleteEntity(mind);
                SEntMan.DeleteEntity(target);
                timers.Remove(session);
            }
            var before = SGameTiming.RealTime;
            first = chat.TryConsumeOrbitraEmote(session);
            deadline = timers[session];
            duration = deadline - before;
            second = chat.TryConsumeOrbitraEmote(session);
            afterRejection = timers[session];
            timers[session] = SGameTiming.RealTime;
            expired = chat.TryConsumeOrbitraEmote(session);
        });
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(duration.TotalSeconds, Is.EqualTo(2).Within(0.05));
            Assert.That(afterRejection, Is.EqualTo(deadline));
            Assert.That(expired, Is.True);
            Assert.That(afterMenu, Is.EqualTo(1));
            Assert.That(afterBlockedInputs, Is.EqualTo(1));
            Assert.That(afterAutomatic, Is.EqualTo(2));
            Assert.That(afterText, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task AntagonistMetadataUsesMindRolesWithoutGrantingWarpAccess()
    {
        GhostWarp ordinary = default, antagonist = default;
        bool lobbyCanWarp = true;
        await Server.WaitPost(() =>
        {
            var ghosts = SEntMan.System<GhostSystem>();
            var target = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var mind = SEntMan.System<MindSystem>().CreateMind(null, "Test");
            SEntMan.System<MindSystem>().TransferTo(mind, target);
            var create = typeof(GhostSystem).GetMethod("CreateOrbitraPlayerWarp", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ordinary = (GhostWarp) create.Invoke(ghosts, new object?[] { target, mind.Owner, "Test" })!;
            SEntMan.System<SharedRoleSystem>().MindAddRole(mind, "MindRoleTraitor", silent: true);
            antagonist = (GhostWarp) create.Invoke(ghosts, new object?[] { target, mind.Owner, "Test" })!;
            lobbyCanWarp = ghosts.CanGhostWarp(ServerSession!, out _);
            SEntMan.DeleteEntity(mind);
            SEntMan.DeleteEntity(target);
        });
        Assert.Multiple(() =>
        {
            Assert.That(ordinary.IsAntagonist, Is.False);
            Assert.That(antagonist.IsAntagonist, Is.True);
            Assert.That(lobbyCanWarp, Is.False);
        });
    }

}
