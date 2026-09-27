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
            var tier = controls.OfType<OptionButton>().Single();
            all = cards.Count(c => c.Visible);
            search.SetText(Client.Resolve<ILocalizationManager>().GetString("orbitra-ratvar-scripture-transmission"), true);
            filtered = cards.Count(c => c.Visible);
            disabled = cards.Where(c => c.Visible).SelectMany(All).OfType<Button>().All(b => b.Disabled);
            search.SetText("no-matching-ratvar-scripture-7284", true);
            missing = cards.Count(c => c.Visible);
            tier.SelectId(3);
            search.SetText(string.Empty, true);
            tiers = cards.Count(c => c.Visible) - scriptures.Count(s => s.Tier == 3);
            window.Measure(new Vector2(580, 640));
            window.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(580, 640)));
            fits = search.Width > 100 && search.Width < window.Width;
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
