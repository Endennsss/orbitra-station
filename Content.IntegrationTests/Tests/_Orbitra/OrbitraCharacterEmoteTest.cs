using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._Orbitra.Stylesheets;
using Content.Client._Orbitra.UserInterface;
using Content.Client.Lobby.UI;
using Content.Client.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraCharacterEmoteTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true, NoLoadTestPrototypes = true };

    private async Task Click(BaseButton button)
    {
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
            await Client.DoGuiEvent(button, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, state,
                new ScreenCoordinates(button.GlobalPixelPosition + button.PixelSize / 2, button.Window!.Id),
                false, button.Size / 2, button.PixelSize / 2));
    }

    [Test]
    public async Task CharacterCardKeepsNameAndDeletionConfirmationSeparate()
    {
        CharacterPickerButton card = null!;
        var deletions = 0;
        await Client.WaitPost(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();
            profile.Name = "Очень длинное имя персонажа для проверки карточки";
            card = new CharacterPickerButton(Client.Resolve<IPrototypeManager>(), Client.Resolve<ISharedPlayerManager>(),
                new ButtonGroup(), profile, false);
            card.OnDeletePressed += () => deletions++;
            Client.Resolve<IUserInterfaceManager>().StateRoot.AddChild(card);
        });
        try
        {
            await Pair.RunTicksSync(2);
            foreach (var width in new[] { 200f, 272f, 340f })
            {
                await Client.WaitPost(() =>
                {
                    card.Measure(new Vector2(width, 400));
                    card.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(width, card.DesiredSize.Y)));
                });
                await Client.WaitAssertion(() =>
                {
                    var name = card.FindControl<Label>("DescriptionLabel");
                    var delete = card.FindControl<OrbitraButton>("DeleteButton");
                    Assert.That(card.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var box), Is.True);
                    Assert.That(box, Is.TypeOf<StyleBoxFlat>());
                    Assert.That(name.Width, Is.GreaterThan(40));
                    Assert.That(delete.GlobalPosition.Y, Is.GreaterThanOrEqualTo(name.GlobalPosition.Y + name.Height));
                    Assert.That(delete.Height, Is.EqualTo(32));
                    Assert.That(name.ToolTip, Does.Contain("Очень длинное"));
                });
            }
            await Click(card.FindControl<OrbitraButton>("DeleteButton"));
            await Client.WaitAssertion(() =>
            {
                Assert.That(deletions, Is.Zero);
                Assert.That(card.FindControl<OrbitraButton>("ConfirmDeleteButton").Visible, Is.True);
            });
            await Pair.RunTicksSync(2);
            await Click(card.FindControl<OrbitraButton>("ConfirmDeleteButton"));
            Assert.That(deletions, Is.EqualTo(1));
        }
        finally
        {
            await Client.WaitPost(() => card.Dispose());
        }
    }

    [Test]
    public async Task EmoteWheelStylesOnlyOptedInMenusAndKeepsNavigationAndReducedMotion()
    {
        SimpleRadialMenu menu = null!;
        SimpleRadialMenu vanilla = null!;
        RadialContainer layer = null!;
        OrbitraRadialSector action = null!;
        var calls = 0;
        var moved = false;
        Color startColor = default;
        Color intermediateColor = default;
        await Client.WaitPost(() =>
        {
            var models = new RadialMenuOptionBase[]
            {
                new RadialMenuNestedLayerOption(new RadialMenuOptionBase[]
                {
                    new RadialMenuActionOption<int>(_ => calls++, 0) { ToolTip = "Action" },
                }) { ToolTip = "Category" },
            };
            menu = new SimpleRadialMenu();
            Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, false);
            menu.EnableOrbitraEmotes();
            menu.SetButtons(models);
            menu.OpenCentered();
            vanilla = new SimpleRadialMenu();
            vanilla.SetButtons(models);
        });
        try
        {
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                Assert.That(vanilla.CloseButtonStyleClass, Is.EqualTo("RadialMenuCloseButton"));
                Assert.That(vanilla.Children.OfType<RadialContainer>().SelectMany(c => c.Children).Any(c => c is OrbitraRadialSector), Is.False);
                Assert.That(menu.ContextualButton.HasStyleClass("OrbitraRadialClose"), Is.True);
                Assert.That(menu.Children.OfType<RadialContainer>().SelectMany(c => c.Children).All(c => c is OrbitraRadialSector), Is.True);
            });
            await Client.WaitPost(() =>
            {
                layer = menu.Children.OfType<RadialContainer>().Single(c => !c.Visible);
                moved = menu.TryToMoveToNewLayer(layer);
            });
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                Assert.That(moved, Is.True);
                Assert.That(menu.ContextualButton.HasStyleClass("OrbitraRadialBack"), Is.True);
            });
            await Client.WaitPost(() =>
            {
                action = (OrbitraRadialSector)layer.GetChild(0);
                startColor = action.BackgroundColor;
                action.SetClickPressed(true);
                typeof(OrbitraRadialSector).GetMethod("AdvanceSurface", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(action, new object[] { 0.03f, false });
                intermediateColor = action.BackgroundColor;
                Client.Resolve<IConfigurationManager>().SetCVar(CCVars.ReducedMotion, true);
            });
            await Client.WaitAssertion(() =>
            {
                Assert.That(intermediateColor.R, Is.GreaterThan(startColor.R));
                Assert.That(intermediateColor.R, Is.LessThan(OrbitraPalettes.Primary.PressedElement.R));
                Assert.That(action.BackgroundColor.R, Is.EqualTo(OrbitraPalettes.Primary.PressedElement.R).Within(0.00001));
                Assert.That(action.BackgroundColor.A, Is.EqualTo(1));
                Assert.That(menu.Modulate, Is.EqualTo(Color.White));
                Assert.That(layer.Modulate, Is.EqualTo(Color.White));
            });
            await Client.WaitPost(() => action.SetClickPressed(false));
            await Click(action);
            await Client.WaitAssertion(() =>
            {
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(menu.IsOpen, Is.False);
            });
            await Client.WaitPost(() =>
            {
                menu.OpenCentered();
                menu.ReturnToPreviousLayer();
            });
            await Client.WaitAssertion(() => Assert.That(menu.ContextualButton.HasStyleClass("OrbitraRadialClose"), Is.True));
        }
        finally
        {
            await Client.WaitPost(() => { menu.Dispose(); vanilla.Dispose(); });
        }
    }
}
