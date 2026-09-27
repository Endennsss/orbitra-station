using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.Ratvar;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Orbitra.Ratvar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Headless layout and filter regression, not a substitute for in-game visual acceptance.</summary>
[TestFixture]
public sealed class OrbitraRatvarCatalogueTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true, NoLoadTestPrototypes = true };

    [Test]
    public async Task CatalogueSearchAndTierFilterPreserveAuthoritativeAvailability()
    {
        var all = 0;
        var filtered = 0;
        var missing = 0;
        var tiers = 0;
        var disabled = false;
        var fits = false;
        var numbers = false;
        var filtersFit = false;
        await Client.WaitPost(() =>
        {
            using var window = new OrbitraRatvarWindow();
            var scriptures = CProtoMan.EnumeratePrototypes<OrbitraRatvarScripturePrototype>().ToArray();
            window.Populate(scriptures);
            window.UpdateCult(new OrbitraRatvarUiState(200, 1, 0, false, 10, 5)
            {
                Unavailable = { ["OrbitraRatvarTransmissionSigil"] = "orbitra-ratvar-unavailable-invokers" },
            });
            window.OpenCentered();
            var controls = All(window).ToArray();
            var cards = controls.OfType<PanelContainer>().Where(c => c.HasStyleClass("OrbitraRatvarCard")).ToArray();
            var search = controls.OfType<LineEdit>().First();
            var tier = controls.OfType<OptionButton>().First();
            all = cards.Count(c => c.Visible) - 1;
            search.SetText(Client.Resolve<ILocalizationManager>().GetString("orbitra-ratvar-scripture-transmission"), true);
            filtered = cards.Count(c => c.Visible) - 1;
            disabled = controls.OfType<Button>().Single(b => b.Text == Client.Resolve<ILocalizationManager>().GetString("orbitra-ratvar-recite")).Disabled;
            search.SetText("no-matching-ratvar-scripture-7284", true);
            missing = cards.Count(c => c.Visible) - 1;
            tier.SelectId(3);
            search.SetText(string.Empty, true);
            tiers = cards.Count(c => c.Visible) - 1 - scriptures.Count(s => s.Tier == 3);
            window.Measure(new Vector2(800, 620));
            window.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(800, 620)));
            fits = search.Width > 100 && search.Width < window.Width;
            filtersFit = controls.OfType<OptionButton>().All(b => b.Width >= b.MinWidth);
            numbers = controls.OfType<RichTextLabel>().Any(l => l.Text != null &&
                l.Text.Contains("10") && l.Text.Contains("5") && !l.Text.Contains("NUMBER"));
            window.Close();
        });
        Assert.Multiple(() =>
        {
            Assert.That(all, Is.GreaterThan(10));
            Assert.That(filtered, Is.EqualTo(1));
            Assert.That(missing, Is.Zero);
            Assert.That(tiers, Is.Zero);
            Assert.That(disabled, Is.True);
            Assert.That(fits, Is.True);
            Assert.That(filtersFit, Is.True);
            Assert.That(numbers, Is.True);
        });
    }

    private static IEnumerable<Control> All(Control root)
    {
        yield return root;
        foreach (var child in root.Children)
        foreach (var nested in All(child))
            yield return nested;
    }
}
