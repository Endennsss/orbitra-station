using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.Ratvar;
using Content.Client.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Ratvar;
using Content.Shared._Orbitra.Ratvar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Large catalogues and long endpoint names must scroll inside a bounded initial window.</summary>
[TestFixture]
public sealed class OrbitraRatvarWindowSizeTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true, NoLoadTestPrototypes = true };

    [Test]
    public async Task BrassScriptureAndFabricatorHaveEqualPerSheetCost()
    {
        await Server.WaitAssertion(() =>
        {
            var scripture = SProtoMan.Index<OrbitraRatvarScripturePrototype>("OrbitraRatvarBrass");
            var tool = (OrbitraRatvarFabricatorComponent) SProtoMan.Index<EntityPrototype>("OrbitraRatvarFabricator")
                .Components["OrbitraRatvarFabricator"].Component;
            Assert.That(scripture.Result?.Id, Is.EqualTo("SheetBrass10"));
            Assert.That(scripture.Energy, Is.EqualTo(tool.BrassEnergy * 10));
        });
    }

    [TestCase("tablet", 760, 560)]
    [TestCase("cache", 500, 420)]
    [TestCase("travel", 500, 420)]
    public async Task ContentDoesNotInflateWindow(string kind, int width, int height)
    {
        Vector2 preferred = default;
        var fits = false;
        var scrolls = false;
        var vitalityVisible = kind != "tablet";
        await Client.WaitPost(() =>
        {
            using FancyWindow window = kind switch
            {
                "tablet" => new OrbitraRatvarWindow(),
                "cache" => new OrbitraRatvarCacheWindow(),
                _ => new OrbitraRatvarTravelWindow(),
            };
            switch (window)
            {
                case OrbitraRatvarWindow tablet:
                    tablet.Populate(CProtoMan.EnumeratePrototypes<OrbitraRatvarScripturePrototype>());
                    tablet.UpdateCult(new OrbitraRatvarUiState(10000, 3, 100, false, 999, 999, 123));
                    break;
                case OrbitraRatvarCacheWindow cache:
                    cache.Update(new OrbitraRatvarCacheUiState("orbitra-ratvar-cache-ready", 0,
                        Enumerable.Repeat("OrbitraRatvarSpectacles", 30).ToArray()));
                    break;
                case OrbitraRatvarTravelWindow travel:
                    var points = new Dictionary<NetEntity, string>();
                    for (var i = 1; i <= 80; i++) points.Add(new NetEntity(i), new string('W', 64));
                    travel.Update(new OrbitraRatvarTravelUiState("Test", 10000, 200,
                        "orbitra-ratvar-travel-ready", points));
                    break;
            }
            preferred = window.SetSize;
            window.OpenCentered();
            window.Measure(new Vector2(width, height));
            window.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(width, height)));
            var controls = All(window).ToArray();
            if (kind == "tablet")
                vitalityVisible = controls.OfType<RichTextLabel>().Any(l => l.Visible && l.Text?.Contains("123") == true);
            var containers = controls.OfType<ScrollContainer>().ToArray();
            scrolls = containers.Length > 0 && containers.All(c => !c.HScrollEnabled);
            fits = window.Width <= width && window.Height <= height &&
                controls.OfType<Button>().All(b => b.Width <= width) &&
                controls.OfType<RichTextLabel>().All(l => l.Width <= width);
            window.Close();
        });
        Assert.Multiple(() =>
        {
            Assert.That(preferred, Is.EqualTo(new Vector2(width, height)), "Explicit bounded initial size");
            Assert.That(scrolls, Is.True, "Long content needs vertical scrolling, not an unbounded horizontal measure");
            Assert.That(fits, Is.True, "Text must remain inside the window");
            Assert.That(vitalityVisible, Is.True, "The tablet shows its private vitality reserve");
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
