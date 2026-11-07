// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.Research.Bluespace;
using Content.Client._Orbitra.Stylesheets;
using Content.Client._Orbitra.UserInterface;
using Content.IntegrationTests.Pair;
using Content.Shared._Orbitra.Research.Bluespace;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra.Research;

/// <summary>Loads the device XAML and checks server snapshot presentation on the headless client thread.</summary>
[TestFixture, NonParallelizable]
public sealed class OrbitraBluespaceDeviceUiTest
{
    /// <summary>Reward cards created after opening inherit the same Orbitra surfaces and control styling.</summary>
    [Test]
    public async Task DeviceUsesOrbitraThemeForDynamicRewardPreview()
    {
        await WithWindow(async (pair, window) =>
        {
            foreach (var category in new[] { OrbitraBluespaceLootCategory.Materials, OrbitraBluespaceLootCategory.Research })
            {
                await pair.Client.WaitPost(() => window.UpdateState(State(OrbitraBluespaceDeviceKind.Harvester, category: category)));
                await pair.RunTicksSync(2);
                await pair.Client.WaitAssertion(() =>
                {
                    Assert.That(window.HasStyleClass("OrbitraEntryWindow"), Is.True);
                    Assert.That(window.FindControl<Button>("EnableButton"), Is.InstanceOf<OrbitraButton>());
                    foreach (var name in new[] { "PowerMode", "LootCategory" })
                    {
                        var option = window.FindControl<OptionButton>(name);
                        Assert.That(option, Is.InstanceOf<OrbitraOptionButton>());
                        Assert.That(option.HasStyleClass("OrbitraEditorControl"), Is.True);
                    }
                    var cards = window.FindControl<BoxContainer>("LootPreview").Children.OfType<PanelContainer>().ToArray();
                    Assert.That(cards, Is.Not.Empty);
                    foreach (var card in cards)
                    {
                        Assert.That(card.HasStyleClass("OrbitraEditorControl"), Is.True);
                        Assert.That(card.PanelOverride, Is.Null);
                        Assert.That(card.TryGetStyleProperty<StyleBox>(PanelContainer.StylePropertyPanel, out var style), Is.True);
                        Assert.That(style, Is.TypeOf<StyleBoxFlat>());
                        Assert.That(((StyleBoxFlat) style!).BackgroundColor, Is.EqualTo(OrbitraPalettes.PanelInset));
                        Assert.That(Descendants(card).All(child => child.HasStyleClass("OrbitraEditorControl")), Is.True);
                    }
                });
            }
        });
    }

    /// <summary>Each device exposes its relevant localized controls without turning snapshots into operating requests.</summary>
    [TestCase(OrbitraBluespaceDeviceKind.Harvester)]
    [TestCase(OrbitraBluespaceDeviceKind.Prober)]
    [TestCase(OrbitraBluespaceDeviceKind.Suppressor)]
    public async Task DeviceSnapshotsUpdateLocalizedControlsWithoutSendingRequests(OrbitraBluespaceDeviceKind kind)
    {
        await WithWindow(async (pair, window) =>
        {
            var client = pair.Client;
            var loc = client.ResolveDependency<ILocalizationManager>();
            var requests = 0;
            await client.WaitPost(() =>
            {
                window.EnabledRequested += _ => requests++;
                window.PowerModeRequested += _ => requests++;
                window.LootCategoryRequested += _ => requests++;
                window.UpdateState(State(kind, enabled: false, mode: OrbitraBluespacePowerMode.Normal,
                    category: OrbitraBluespaceLootCategory.Materials));
                window.UpdateState(State(kind, mode: OrbitraBluespacePowerMode.Overdrive,
                    category: OrbitraBluespaceLootCategory.Research));
            });
            await client.WaitAssertion(() =>
            {
                var nameKey = $"orbitra-bluespace-device-{kind.ToString().ToLowerInvariant()}";
                var instructionsKey = $"orbitra-bluespace-instructions-{kind.ToString().ToLowerInvariant()}";
                Assert.That(loc.HasString(nameKey) && loc.HasString(instructionsKey), Is.True);
                Assert.That(window.Title, Is.EqualTo(loc.GetString(nameKey)));
                Assert.That(window.FindControl<Label>("DeviceName").Text, Is.EqualTo(loc.GetString(nameKey)));
                Assert.That(window.FindControl<RichTextLabel>("Instructions").Text, Is.EqualTo(loc.GetString(instructionsKey)));
                Assert.That(window.FindControl<Label>("Status").Text, Is.EqualTo(loc.GetString("orbitra-bluespace-status-working")));
                Assert.That(window.FindControl<BoxContainer>("HarvesterPanel").Visible,
                    Is.EqualTo(kind == OrbitraBluespaceDeviceKind.Harvester));
                Assert.That(window.FindControl<Label>("ResearchRate").Visible,
                    Is.EqualTo(kind == OrbitraBluespaceDeviceKind.Prober));
                Assert.That(window.FindControl<Label>("ResearchRate").Text,
                    Is.EqualTo(loc.GetString("orbitra-bluespace-research-rate", ("rate", 120f))));
                Assert.That(window.FindControl<ProgressBar>("Background").Value, Is.EqualTo(42.5f));
                Assert.That(window.FindControl<ProgressBar>("Progress").Value, Is.EqualTo(0.625f));
                Assert.That(window.FindControl<RichTextLabel>("PowerText").Text,
                    Is.EqualTo(loc.GetString("orbitra-bluespace-power-value", ("received", 1000f), ("requested", 1000f))));
                var enable = window.FindControl<Button>("EnableButton");
                Assert.That(enable.Text, Is.EqualTo(loc.GetString("orbitra-bluespace-disable")));
                Assert.That(enable.Disabled, Is.False);
                var mode = window.FindControl<OptionButton>("PowerMode");
                var category = window.FindControl<OptionButton>("LootCategory");
                Assert.That(mode.SelectedId, Is.EqualTo((int) OrbitraBluespacePowerMode.Overdrive));
                Assert.That(category.SelectedId, Is.EqualTo((int) OrbitraBluespaceLootCategory.Research));
                Assert.That(Descendants(mode).OfType<Label>().Any(label =>
                    label.Text?.Trim() == loc.GetString("orbitra-bluespace-mode-overdrive")), Is.True);
                Assert.That(Descendants(category).OfType<Label>().Any(label =>
                    label.Text?.Trim() == loc.GetString("orbitra-bluespace-category-research")), Is.True);
                Assert.That(requests, Is.Zero, "Applying authoritative snapshots must never request machine changes.");
            });
        });
    }

    /// <summary>Research devices require full working power while harvesters accumulate partial power.</summary>
    [TestCase(OrbitraBluespaceDeviceKind.Harvester, "working")]
    [TestCase(OrbitraBluespaceDeviceKind.Prober, "no-power")]
    [TestCase(OrbitraBluespaceDeviceKind.Suppressor, "no-power")]
    public async Task PartialPowerDoesNotReportResearchDeviceAsWorking(OrbitraBluespaceDeviceKind kind, string status)
    {
        await WithWindow(async (pair, window) =>
        {
            var loc = pair.Client.ResolveDependency<ILocalizationManager>();
            await pair.Client.WaitPost(() => window.UpdateState(State(kind, received: 125_000f, requested: 250_000f)));
            await pair.Client.WaitAssertion(() => Assert.That(window.FindControl<Label>("Status").Text,
                Is.EqualTo(loc.GetString($"orbitra-bluespace-status-{status}"))));
        });
    }

    /// <summary>ETA uses received watts and the remaining configured energy, including overdrive and partial power.</summary>
    [TestCase(250_000f, 250_000f, 0f, 60d)]
    [TestCase(500_000f, 500_000f, 0f, 30d)]
    [TestCase(125_000f, 250_000f, 0.5f, 60d)]
    public async Task HarvesterEtaUsesActualPowerAndRemainingMegajoules(float received, float requested, float progress, double seconds)
    {
        await WithWindow(async (pair, window) =>
        {
            var loc = pair.Client.ResolveDependency<ILocalizationManager>();
            await pair.Client.WaitPost(() => window.UpdateState(State(OrbitraBluespaceDeviceKind.Harvester,
                received: received, requested: requested, progress: progress)));
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(window.FindControl<RichTextLabel>("Eta").Text,
                    Is.EqualTo(loc.GetString("orbitra-bluespace-eta-value", ("seconds", seconds))));
                Assert.That(window.FindControl<Label>("ProgressText").Text,
                    Is.EqualTo(loc.GetString("orbitra-bluespace-energy-value", ("stored", progress * 15),
                        ("required", 15f), ("percent", progress * 100))));
            });
        });
    }

    /// <summary>Decimal UI measurements stay concise after Fluent converts numeric arguments.</summary>
    [Test]
    public async Task DecimalMeasurementsDoNotExposeBinaryFloatTails()
    {
        await WithWindow(async (pair, window) =>
        {
            var loc = pair.Client.ResolveDependency<ILocalizationManager>();
            await pair.Client.WaitPost(() => window.UpdateState(State(OrbitraBluespaceDeviceKind.Harvester,
                received: 249_600f, requested: 250_600f, progress: 0.64f, background: 42.6f)));
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(window.FindControl<Label>("ProgressText").Text,
                    Is.EqualTo(loc.GetString("orbitra-bluespace-energy-value", ("stored", 9.6d), ("required", 15d), ("percent", 64d))));
                Assert.That(window.FindControl<RichTextLabel>("PowerText").Text,
                    Is.EqualTo(loc.GetString("orbitra-bluespace-power-value", ("received", 249.6d), ("requested", 250.6d))));
                Assert.That(window.FindControl<Label>("BackgroundText").Text,
                    Is.EqualTo(loc.GetString("orbitra-bluespace-background-value", ("level", 42.6d))));
            });
            await pair.Client.WaitPost(() => window.UpdateState(State(OrbitraBluespaceDeviceKind.Prober,
                received: 249_600f, requested: 250_600f, pointsPerMinute: 120.1f)));
            await pair.Client.WaitAssertion(() => Assert.That(window.FindControl<Label>("ResearchRate").Text,
                Is.EqualTo(loc.GetString("orbitra-bluespace-research-rate", ("rate", 120.1d)))));
        });
    }

    /// <summary>Waiting and emergency states explain the operator action and do not promise a running ETA.</summary>
    [Test]
    public async Task BlockingSnapshotsShowActionHintsAndSuppressorRemainsAvailableDuringCriticalBackground()
    {
        await WithWindow(async (pair, window) =>
        {
            var loc = pair.Client.ResolveDependency<ILocalizationManager>();
            foreach (var (state, status, disabled) in new[]
                     {
                         (State(OrbitraBluespaceDeviceKind.Harvester, enabled: false), "off", false),
                         (State(OrbitraBluespaceDeviceKind.Harvester, enabled: false, anchored: false), "unanchored", true),
                         (State(OrbitraBluespaceDeviceKind.Harvester, enabled: false, hasStation: false), "no-station", true),
                         (State(OrbitraBluespaceDeviceKind.Harvester, enabled: false, critical: true), "critical", true),
                         (State(OrbitraBluespaceDeviceKind.Harvester, blocked: true, progress: 1), "blocked", false),
                         (State(OrbitraBluespaceDeviceKind.Harvester, powered: false, received: 0), "no-power", false),
                         (State(OrbitraBluespaceDeviceKind.Suppressor, critical: true), "working", false),
                     })
            {
                await pair.Client.WaitPost(() => window.UpdateState(state));
                await pair.Client.WaitAssertion(() =>
                {
                    var hint = status == "working" ? "orbitra-bluespace-hint-working-suppressor" : $"orbitra-bluespace-hint-{status}";
                    Assert.That(window.FindControl<Label>("Status").Text, Is.EqualTo(loc.GetString($"orbitra-bluespace-status-{status}")));
                    Assert.That(window.FindControl<RichTextLabel>("StatusHint").Text, Is.EqualTo(loc.GetString(hint)));
                    Assert.That(window.FindControl<Button>("EnableButton").Disabled, Is.EqualTo(disabled));
                    Assert.That(window.FindControl<RichTextLabel>("Eta").Text, Is.EqualTo(loc.GetString("orbitra-bluespace-eta-paused")));
                });
            }
        });
    }

    /// <summary>Weighted previews expose complete batches and survive polling, open help and scrolling.</summary>
    [Test]
    public async Task RewardPreviewKeepsRowsScrollAndHelpWhileMeasurementsUpdate()
    {
        await WithWindow(async (pair, window) =>
        {
            var loc = pair.Client.ResolveDependency<ILocalizationManager>();
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var viewport = new Vector2(640, 450);
            var preview = window.FindControl<BoxContainer>("LootPreview");
            var scroll = window.FindControl<ScrollContainer>("DeviceScroll");
            var help = window.FindControl<Button>("InstructionsButton");
            Control[] rows = [];
            var position = Vector2.Zero;
            await pair.Client.WaitPost(() =>
            {
                window.UpdateState(State(OrbitraBluespaceDeviceKind.Harvester));
                window.FitToViewport(viewport);
                ArrangeWindow(window, viewport);
            });
            await Click(pair, help);
            await pair.Client.WaitPost(() =>
            {
                ArrangeWindow(window, viewport);
                scroll.SetScrollValue(new Vector2(0, 100));
                position = scroll.GetScrollValue();
                rows = preview.Children.ToArray();
                window.UpdateState(State(OrbitraBluespaceDeviceKind.Harvester, received: 125_000f, progress: 0.75f));
                ArrangeWindow(window, viewport);
            });
            await pair.Client.WaitAssertion(() =>
            {
                var rewards = prototypes.EnumeratePrototypes<OrbitraBluespaceHarvesterLootPrototype>()
                    .Where(reward => reward.Category == OrbitraBluespaceLootCategory.Materials).ToArray();
                var total = rewards.Sum(reward => (double) reward.Weight);
                var chances = Descendants(preview).OfType<Label>().Select(label => label.Text).ToArray();
                var products = Descendants(preview).OfType<RichTextLabel>().Select(label => label.Text).ToArray();
                Assert.That(preview.ChildCount, Is.EqualTo(rewards.Length));
                foreach (var reward in rewards)
                {
                    Assert.That(chances, Does.Contain(loc.GetString("orbitra-bluespace-preview-chance",
                        ("chance", Math.Round(reward.Weight / total * 100, 1)))));
                    foreach (var (product, count) in reward.Products)
                        Assert.That(products, Does.Contain(loc.GetString("orbitra-bluespace-preview-product",
                            ("count", count), ("name", prototypes.Index(product).Name))));
                }
                Assert.That(preview.Children, Is.EqualTo(rows), "Polling must not rebuild recipe controls.");
                Assert.That(position.Y, Is.GreaterThan(0));
                Assert.That(scroll.GetScrollValue(), Is.EqualTo(position));
                Assert.That(help.Pressed, Is.True);
                Assert.That(window.FindControl<BoxContainer>("HelpPanel").Visible, Is.True);
            });
            await pair.Client.WaitPost(() => window.UpdateState(State(OrbitraBluespaceDeviceKind.Harvester,
                category: OrbitraBluespaceLootCategory.Research)));
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(preview.Children, Is.Not.EqualTo(rows));
                Assert.That(preview.ChildCount, Is.EqualTo(prototypes.EnumeratePrototypes<OrbitraBluespaceHarvesterLootPrototype>()
                    .Count(reward => reward.Category == OrbitraBluespaceLootCategory.Research)));
            });
        });
    }

    /// <summary>Primary controls stay visible and every data panel fits horizontally at small logical viewport sizes.</summary>
    [TestCase(OrbitraBluespaceDeviceKind.Harvester)]
    [TestCase(OrbitraBluespaceDeviceKind.Prober)]
    [TestCase(OrbitraBluespaceDeviceKind.Suppressor)]
    public async Task DeviceWindowsFitSmallViewport(OrbitraBluespaceDeviceKind kind)
    {
        await WithWindow(async (pair, window) =>
        {
            foreach (var viewport in new[] { new Vector2(1280, 720), new Vector2(640, 360), new Vector2(480, 360) })
            {
                await pair.Client.WaitPost(() =>
                {
                    window.UpdateState(State(kind));
                    window.FitToViewport(viewport);
                    ArrangeWindow(window, viewport);
                });
                await pair.Client.WaitAssertion(() =>
                {
                    Assert.That(window.Width, Is.LessThanOrEqualTo(viewport.X));
                    Assert.That(window.Height, Is.LessThanOrEqualTo(viewport.Y));
                    foreach (var name in new[] { "Status", "StatusHint", "EnableButton", "InstructionsButton", "DeviceScroll" })
                        AssertInside(window, window.FindControl<Control>(name));
                    var scroll = window.FindControl<ScrollContainer>("DeviceScroll");
                    Assert.That(scroll.Height, Is.GreaterThan(0));
                    foreach (var content in Descendants(scroll).Where(control => control.VisibleInTree &&
                                 control is Label or RichTextLabel or ProgressBar or OptionButton))
                    {
                        var position = content.GlobalPosition - scroll.GlobalPosition;
                        Assert.That(position.X, Is.GreaterThanOrEqualTo(-1), content.Name);
                        Assert.That(position.X + content.Width, Is.LessThanOrEqualTo(scroll.Width + 1), content.Name);
                    }
                });
            }
        });
    }

    /// <summary>A prober without a linked server explains that prerequisite before its secondary lack of working power.</summary>
    [Test]
    public async Task EnabledProberWithoutServerShowsMissingServerInsteadOfMissingPower()
    {
        await WithWindow(async (pair, window) =>
        {
            var client = pair.Client;
            var loc = client.ResolveDependency<ILocalizationManager>();
            await client.WaitPost(() => window.UpdateState(State(OrbitraBluespaceDeviceKind.Prober,
                powered: false, hasResearchServer: false)));
            await client.WaitAssertion(() =>
            {
                var status = window.FindControl<Label>("Status").Text;
                Assert.That(status, Is.EqualTo(loc.GetString("orbitra-bluespace-status-no-server")));
                Assert.That(status, Is.Not.EqualTo(loc.GetString("orbitra-bluespace-status-no-power")));
                var enable = window.FindControl<Button>("EnableButton");
                Assert.That(enable.Text, Is.EqualTo(loc.GetString("orbitra-bluespace-disable")));
                Assert.That(enable.Disabled, Is.False, "The operator must still be able to switch off the waiting prober.");
            });
        });
    }

    private static OrbitraBluespaceDeviceBoundUserInterfaceState State(OrbitraBluespaceDeviceKind kind,
        bool enabled = true, bool powered = true, bool hasResearchServer = true,
        OrbitraBluespacePowerMode mode = OrbitraBluespacePowerMode.Normal,
        OrbitraBluespaceLootCategory category = OrbitraBluespaceLootCategory.Materials,
        float received = 1_000_000f, float requested = 1_000_000f, float progress = 0.625f,
        bool anchored = true, bool hasStation = true, bool critical = false, bool blocked = false,
        float background = 42.5f, float pointsPerMinute = 120f)
    {
        return new OrbitraBluespaceDeviceBoundUserInterfaceState(kind, enabled, powered, anchored: anchored,
            hasStation: hasStation, hasResearchServer: hasResearchServer, critical: critical, background: background,
            receivedPower: received, requestedPower: requested, progress: progress, pointsPerMinute: pointsPerMinute,
            powerMode: mode, lootCategory: category, outputBlocked: blocked, energyPerReward: 15_000_000f);
    }

    private static void ArrangeWindow(Control window, Vector2 viewport)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var control in Descendants(window).Reverse().Append(window))
            {
                control.InvalidateMeasure();
                control.InvalidateArrange();
            }
            window.Measure(viewport);
            window.Arrange(UIBox2.FromDimensions(window.Position, window.SetSize));
        }
    }

    private static void AssertInside(Control window, Control control)
    {
        var position = control.GlobalPosition - window.GlobalPosition;
        Assert.That(position.X, Is.GreaterThanOrEqualTo(-1));
        Assert.That(position.Y, Is.GreaterThanOrEqualTo(-1));
        Assert.That(position.X + control.Width, Is.LessThanOrEqualTo(window.Width + 1));
        Assert.That(position.Y + control.Height, Is.LessThanOrEqualTo(window.Height + 1));
    }

    private static async Task Click(TestPair pair, BaseButton control)
    {
        // Orbitra-Edit: отложенное центрирование и раскладка должны завершиться до фиксации экранной точки клика.
        await pair.RunTicksSync(2);
        var position = control.Size / 2;
        var pixelPosition = position * control.UIScale;
        var screen = new ScreenCoordinates(control.GlobalPixelPosition + pixelPosition, control.Window?.Id ?? default);
        await pair.Client.DoGuiEvent(control, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick,
            BoundKeyState.Down, screen, default, position, pixelPosition));
        await pair.RunTicksSync(1);
        await pair.Client.DoGuiEvent(control, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick,
            BoundKeyState.Up, screen, default, position, pixelPosition));
        await pair.RunTicksSync(1);
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (var child in parent.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static async Task WithWindow(Func<TestPair, OrbitraBluespaceDeviceWindow, Task> scenario)
    {
        // Orbitra-Edit: портреты и иконки используют клиентские EntitySystem, которые запускаются после подключения.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        OrbitraBluespaceDeviceWindow window = null!;
        try
        {
            // Orbitra-Edit: окно получает снимки напрямую; все операции выполняются на подключённом клиентском потоке.
            await pair.Client.WaitPost(() =>
            {
                window = new OrbitraBluespaceDeviceWindow();
                window.OpenCentered();
            });
            await scenario(pair, window);
        }
        finally
        {
            await pair.Client.WaitPost(() => window?.Orphan());
        }
        await pair.CleanReturnAsync();
    }
}
