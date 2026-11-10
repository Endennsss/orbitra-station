// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Content.Client.Cargo.UI;
using Content.Client.MassMedia.Ui;
using Content.Client.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Cargo;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed class OrbitraActionLayoutTest : GameTest
{
    [Test]
    public async Task LocalizedActionsFitNarrowWindowsAndReflowOnResize()
    {
        await Client.WaitAssertion(() =>
        {
            var loc = Client.ResolveDependency<ILocalizationManager>();
            var previousCulture = loc.DefaultCulture;
            loc.SetCulture(CultureInfo.GetCultureInfo("ru-RU"));
            try
            {
                using var cargo = new FancyWindow();
                var order = new CargoOrderRow();
                order.Title.SetMessage("Доставка еды хэппи хонк большой укус (x1 за 2800$)");
                cargo.ContentsContainer.AddChild(order);
                cargo.OpenCentered();
                Arrange(cargo, 280);
                AssertButtonsFit(order, order.Approve, order.Cancel);
                var narrowTitleHeight = order.Title.Height;
                Arrange(cargo, 650);
                Assert.That(order.Title.Height, Is.LessThan(narrowTitleHeight));

                using var bounty = new CargoBountyMenu();
                bounty.UpdateEntries([new CargoBountyData { Bounty = "BountyTrash", Id = "NT001" }], [], TimeSpan.Zero);
                bounty.OpenCentered();
                Arrange(bounty, 400);
                var bountyEntry = bounty.FindControl<BoxContainer>("BountyEntriesContainer").GetChild(0);
                AssertButtonsFit(bountyEntry, bountyEntry.FindControl<Button>("PrintButton"), bountyEntry.FindControl<Button>("SkipButton"));

                using var news = new NewsWriterMenu();
                news.OpenCentered();
                var editor = news.ArticleEditorPanel;
                editor.Visible = true;
                var cancel = editor.FindControl<Button>("ButtonCancel");
                var save = editor.FindControl<Button>("ButtonSaveDraft");
                var preview = editor.FindControl<Button>("ButtonPreview");
                Arrange(news, 348);
                Assert.That(news.FindControl<BoxContainer>("ArticleListPanel").Visible, Is.False);
                AssertButtonsFit(editor, cancel, save, preview, editor.ButtonPublish);
                Assert.That(editor.ButtonPublish.GlobalPosition.Y, Is.GreaterThan(cancel.GlobalPosition.Y));

                Arrange(news, 760);
                AssertButtonsFit(editor, cancel, save, preview, editor.ButtonPublish);
                Assert.That(editor.ButtonPublish.GlobalPosition.Y, Is.EqualTo(cancel.GlobalPosition.Y),
                    $"Window {news.Width}, editor {editor.Width}, row {cancel.Parent!.Width}, widest button {preview.DesiredSize.X}.");
                editor.Visible = false;
                Assert.That(news.FindControl<BoxContainer>("ArticleListPanel").Visible, Is.True);
            }
            finally
            {
                loc.DefaultCulture = previousCulture;
            }
        });
    }

    private static void Arrange(Control window, float width)
    {
        var size = new Vector2(width, 460);
        window.SetSize = size;
        window.Measure(size);
        window.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
    }

    private static void AssertButtonsFit(Control panel, params Button[] buttons)
    {
        var occupied = new List<UIBox2>();
        foreach (var button in buttons)
        {
            var rect = button.GlobalRect;
            Assert.That(button.TryGetStyleProperty<StyleBoxFlat>(ContainerButton.StylePropertyStyleBox, out _), Is.True, button.Text);
            Assert.That(rect.Left, Is.GreaterThanOrEqualTo(panel.GlobalRect.Left - 1), button.Text);
            Assert.That(rect.Right, Is.LessThanOrEqualTo(panel.GlobalRect.Right + 1), button.Text);
            Assert.That(rect.Bottom, Is.LessThanOrEqualTo(panel.GlobalRect.Bottom + 1), button.Text);
            Assert.That(occupied.Exists(other => other.Intersects(rect)), Is.False, button.Text);
            occupied.Add(rect);

            var availableTextWidth = button.Label.Width;
            button.Label.Measure(new Vector2(float.PositiveInfinity));
            Assert.That(availableTextWidth + 1, Is.GreaterThanOrEqualTo(button.Label.DesiredSize.X), button.Text);
        }
    }
}
