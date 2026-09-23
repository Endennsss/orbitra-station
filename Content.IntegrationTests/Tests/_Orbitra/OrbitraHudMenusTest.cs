using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.Stylesheets;
using Content.Client._Orbitra.UserInterface;
using Content.Client.ContextMenu.UI;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Client.Verbs.UI;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Chat;
using Content.Shared.Verbs;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraHudMenusTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true, NoLoadTestPrototypes = true };

    private static IEnumerable<Control> All(Control root)
    {
        yield return root;
        foreach (var child in root.Children)
        foreach (var nested in All(child))
            yield return nested;
    }

    [Test]
    public async Task HudButtonsKeepBindingsAndChatKeepsChannelColorsAcrossWidths()
    {
        GameTopMenuBar bar = null!;
        ChatBox chat = null!;
        var sizes = new List<Vector2>();
        await Client.WaitPost(() =>
        {
            var ui = Client.Resolve<IUserInterfaceManager>();
            bar = new GameTopMenuBar();
            chat = new ChatBox { MinSize = Vector2.Zero };
            ui.StateRoot.AddChild(bar);
            ui.StateRoot.AddChild(chat);
            chat.ChatInput.ChannelSelector.UpdateChannelSelectButton(ChatSelectChannel.OOC, null);
            chat.ChatInput.Input.Text = "draft";
            chat.AddLine("[bold]Colored message[/bold]", Color.MediumPurple);
        });
        try
        {
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                var buttons = All(bar).OfType<MenuButton>().ToArray();
                Assert.That(buttons, Has.Length.EqualTo(9));
                foreach (var button in buttons)
                {
                    Assert.That(button.HasStyleClass("OrbitraHudButton"), Is.True);
                    Assert.That(button.BoundKey, Is.Not.Null);
                    Assert.That(button.ToolTip, Is.Not.Empty);
                    Assert.That(button.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var style), Is.True);
                    Assert.That(style, Is.TypeOf<StyleBoxFlat>());
                    var icon = button.ButtonRoot.Children.OfType<TextureRect>().Single();
                    Assert.That(icon.SetSize, Is.EqualTo(new Vector2(24)));
                    Assert.That(icon.TryGetStyleProperty<Texture>(TextureRect.StylePropertyTexture, out _), Is.True);
                }
                Assert.That(chat.ChatInput.ChannelSelector.Modulate, Is.EqualTo(Color.White));
                Assert.That(chat.ChatInput.ChannelSelector.Label.FontColorOverride, Is.EqualTo(Color.LightSkyBlue));
                Assert.That(chat.ChatInput.FilterButton.SetSize, Is.EqualTo(new Vector2(32)));
                Assert.That(All(chat).OfType<VScrollBar>().All(b => b.HasStyleClass("OrbitraEditorControl")), Is.True);
            });
            await Client.WaitPost(() =>
            {
                foreach (var width in new[] { 360f, 465f, 640f })
                {
                    var available = new Vector2(width, 500);
                    chat.Measure(available);
                    chat.Arrange(UIBox2.FromDimensions(Vector2.Zero, available));
                    sizes.Add(chat.ChatInput.Input.Size);
                }
            });
            await Client.WaitAssertion(() => Assert.That(chat.ChatInput.Input.Text, Is.EqualTo("draft")));
            Assert.That(sizes.All(s => s.X > 100 && s.Y >= 30), Is.True);
            var help = All(bar).OfType<MenuButton>().Single(b => b.Name == "AHelpButton");
            Color before = default;
            await Client.WaitAssertion(() =>
            {
                help.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var style);
                before = ((StyleBoxFlat) style!).BackgroundColor;
            });
            await Client.WaitPost(() => help.AddStyleClass(Content.Client.Stylesheets.StyleClass.Negative));
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                help.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var style);
                Assert.That(((StyleBoxFlat) style!).BackgroundColor, Is.Not.EqualTo(before));
            });
            await Client.WaitPost(() => help.RemoveStyleClass(Content.Client.Stylesheets.StyleClass.Negative));
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                help.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var style);
                Assert.That(((StyleBoxFlat) style!).BackgroundColor, Is.EqualTo(before));
            });
        }
        finally
        {
            await Client.WaitPost(() => { bar.Dispose(); chat.Dispose(); });
        }
    }

    [Test]
    public async Task ChannelPopupStacksAllChannelsAndStaysInsideScreen()
    {
        ChatBox chat = null!;
        await Client.WaitPost(() =>
        {
            chat = new ChatBox { SetSize = new Vector2(465, 225) };
            var ui = Client.Resolve<IUserInterfaceManager>();
            ui.StateRoot.AddChild(chat);
            LayoutContainer.SetPosition(chat, new Vector2(32, Math.Max(32, ui.RootControl.Height - 250)));
            var channels = default(ChatSelectChannel);
            foreach (var channel in Content.Client.UserInterface.Systems.Chat.Controls.ChannelSelectorPopup.ChannelSelectorOrder)
                channels |= channel;
            chat.ChatInput.ChannelSelector.Popup.SetChannels(channels);
        });
        try
        {
            await Pair.RunTicksSync(2);
            var button = chat.ChatInput.ChannelSelector;
            for (var repeat = 0; repeat < 2; repeat++)
            {
                // Headless ticks не увеличивают CurFrame; штатная кнопка блокирует повтор в том же кадре.
                await Client.WaitPost(() => CGameTiming.CurFrame++);
                foreach (var state in new[] { Robust.Shared.Input.BoundKeyState.Down, Robust.Shared.Input.BoundKeyState.Up })
                    await Client.DoGuiEvent(button, new GUIBoundKeyEventArgs(Robust.Shared.Input.EngineKeyFunctions.UIClick, state,
                        new Robust.Shared.Map.ScreenCoordinates(button.GlobalPixelPosition + button.PixelSize / 2, button.Window!.Id),
                        false, button.Size / 2, button.PixelSize / 2));
                await Pair.RunTicksSync(2);
                await Client.WaitAssertion(() =>
                {
                    var popup = button.Popup;
                    Assert.That(popup.Visible, Is.True, $"open {repeat}, pressed={button.Pressed}, styled={popup.HasStyleClass("OrbitraChannelPopup")}");
                    var rows = All(popup).OfType<BoxContainer>().Single(c => c.Name == "OrbitraChannelRows");
                    Assert.That(rows.Orientation, Is.EqualTo(BoxContainer.LayoutOrientation.Vertical));
                    Assert.That(rows.ChildCount, Is.EqualTo(8));
                    Assert.That(popup.Width, Is.LessThanOrEqualTo(240));
                    Assert.That(popup.GlobalPosition.X, Is.GreaterThanOrEqualTo(16));
                    Assert.That(popup.GlobalPosition.Y, Is.GreaterThanOrEqualTo(16));
                    Assert.That(popup.GlobalPosition.Y + popup.Height, Is.LessThanOrEqualTo(button.Root!.Height - 16));
                    foreach (var row in rows.Children.OfType<Button>())
                    {
                        Assert.That(row.ClipText, Is.False);
                        Assert.That(row.Width, Is.GreaterThan(200));
                    }
                });
                await Client.WaitPost(() => button.Popup.Close());
                await Pair.RunTicksSync(2);
            }
        }
        finally
        {
            await Client.WaitPost(() => chat.Dispose());
        }
    }

    [Test]
    public async Task FiltersKeepFullLabelsAndHighlightDraft()
    {
        ChatBox chat = null!;
        await Client.WaitPost(() =>
        {
            chat = new ChatBox { SetSize = new Vector2(465, 225) };
            var ui = Client.Resolve<IUserInterfaceManager>();
            ui.StateRoot.AddChild(chat);
            LayoutContainer.SetPosition(chat, new Vector2(32, ui.RootControl.Height - 250));
            chat.ChatInput.FilterButton.Popup.SetChannels((ChatChannel) ushort.MaxValue);
            chat.ChatInput.FilterButton.Popup.UpdateHighlights("analyzer");
        });
        try
        {
            await Pair.RunTicksSync(2);
            var button = chat.ChatInput.FilterButton;
            await Client.WaitPost(() => CGameTiming.CurFrame++);
            foreach (var state in new[] { Robust.Shared.Input.BoundKeyState.Down, Robust.Shared.Input.BoundKeyState.Up })
                await Client.DoGuiEvent(button, new GUIBoundKeyEventArgs(Robust.Shared.Input.EngineKeyFunctions.UIClick, state,
                    new Robust.Shared.Map.ScreenCoordinates(button.GlobalPixelPosition + button.PixelSize / 2, button.Window!.Id),
                    false, button.Size / 2, button.PixelSize / 2));
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                var popup = button.Popup;
                Assert.That(popup.Visible, Is.True);
                Assert.That(popup.Width, Is.EqualTo(560).Within(1));
                foreach (var check in All(popup).OfType<CheckBox>())
                {
                    Assert.That(check.ClipText, Is.False);
                    Assert.That(check.Width, Is.GreaterThan(220));
                }
                Assert.That(Rope.Collapse(popup.FindControl<TextEdit>("HighlightEdit").TextRope), Is.EqualTo("analyzer"));
                Assert.That(All(chat).Single(c => c.Name == "OrbitraChatHistory").HasStyleClass("OrbitraChatFrame"), Is.True);
            });
        }
        finally
        {
            await Client.WaitPost(() => { chat.ChatInput.FilterButton.Popup.Close(); chat.Dispose(); });
        }
    }

    [Test]
    public async Task QuickEmotesMatchRadialEligibilityAndClearOnDetach()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            var actor = Server.EntMan.SpawnEntity("MobHuman", new Robust.Shared.Map.EntityCoordinates(map.MapUid, Vector2.Zero));
            Server.PlayerMan.SetAttachedEntity(Server.PlayerMan.GetSessionById(Client.Session!.UserId), actor);
        });
        await Pair.RunTicksSync(90);
        OrbitraQuickEmotes panel = null!;
        await Client.WaitPost(() =>
        {
            panel = new OrbitraQuickEmotes();
            Client.Resolve<IUserInterfaceManager>().StateRoot.AddChild(panel);
        });
        try
        {
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                var expected = Client.Resolve<IUserInterfaceManager>().GetUIController<Content.Client.UserInterface.Systems.Emotes.EmotesUIController>()
                    .GetAvailableEmotes().Select(e => e.ID).ToArray();
                Assert.That(expected, Is.Not.Empty);
                var actual = All(panel).OfType<ContainerButton>().Where(b => b.HasStyleClass("OrbitraEmoteButton")).Select(b => b.Name).ToArray();
                Assert.That(actual, Is.EquivalentTo(expected));
            });
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(Server.PlayerMan.GetSessionById(Client.Session!.UserId), null));
            await Pair.RunTicksSync(30);
            await Client.WaitAssertion(() => Assert.That(panel.Visible, Is.False));
        }
        finally
        {
            await Client.WaitPost(() => panel.Dispose());
        }
    }

    [Test]
    public async Task ContextMenusUseFlatStatesAndPreserveUnknownIcons()
    {
        ContextMenuPopup popup = null!;
        VerbMenuElement service = null!;
        VerbMenuElement game = null!;
        ConfirmationMenuElement confirm = null!;
        await Client.WaitPost(() =>
        {
            var ui = Client.Resolve<IUserInterfaceManager>();
            popup = new ContextMenuPopup(ui.GetUIController<ContextMenuUIController>(), null);
            service = new VerbMenuElement(new Verb { Text = "Inspect", Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/vv.svg.192dpi.png")) });
            game = new VerbMenuElement(new Verb { Text = "Game icon", Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/antag-e_sword-temp.192dpi.png")) });
            confirm = new ConfirmationMenuElement(new Verb(), "Confirm");
            popup.MenuBody.AddChild(service);
            popup.MenuBody.AddChild(game);
            popup.MenuBody.AddChild(confirm);
        });
        try
        {
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                Assert.That(All(service).OfType<OrbitraIcon>().Single().Icon, Is.EqualTo("eye"));
                Assert.That(All(game).OfType<OrbitraIcon>(), Is.Empty);
                var panel = All(popup).OfType<PanelContainer>().Single(p => p.HasStyleClass("OrbitraContextPanel"));
                Assert.That(panel.TryGetStyleProperty<StyleBox>(PanelContainer.StylePropertyPanel, out var style), Is.True);
                Assert.That(style, Is.TypeOf<StyleBoxFlat>());
                Assert.That(confirm.HasStyleClass(ConfirmationMenuElement.StyleClassConfirmationContextMenuButton), Is.True);
                Assert.That(service.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out style), Is.True);
                Assert.That(((StyleBoxFlat) style!).BackgroundColor, Is.EqualTo(OrbitraPalettes.PanelInset));
                var disclosure = All(service).OfType<TextureRect>().Single(t => t.HasStyleClass(ContextMenuElement.StyleClassContextMenuExpansionTexture));
                Assert.That(disclosure.TryGetStyleProperty<Texture>(TextureRect.StylePropertyTexture, out var texture), Is.True);
                Assert.That(texture!.Width, Is.EqualTo(32));
            });
            await Client.WaitPost(() => service.Pressed = true);
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                service.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var style);
                Assert.That(((StyleBoxFlat) style!).BackgroundColor, Is.EqualTo(OrbitraPalettes.Primary.PressedElement));
            });
            await Client.WaitPost(() => service.Pressed = false);
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                service.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var style);
                Assert.That(((StyleBoxFlat) style!).BackgroundColor, Is.EqualTo(OrbitraPalettes.PanelInset));
            });
        }
        finally
        {
            await Client.WaitPost(() => popup.Dispose());
        }
    }
}
