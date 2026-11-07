// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Collections.Generic;
using System.Linq;
using Content.Client._Orbitra.Ghost.Skins;
using Content.Client._Orbitra.UserInterface;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Shared._Orbitra.Ghost;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra.Ghost;

[TestFixture, NonParallelizable]
public sealed class OrbitraGhostSkinUiTest
{
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }

    [Test]
    public async Task HudSelectorReflectsNetworkChoiceAndReappliesPreferenceToNewGhost()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var player = pair.Player!;
        var previous = player.AttachedEntity;
        var cfg = client.ResolveDependency<IConfigurationManager>();
        var preference = cfg.GetCVar(OrbitraGhostSkinCVars.SelectedSkin);
        GhostGui gui = null!;
        EntityUid ghost = default, replacement = default;
        OrbitraGhostSkinWindow window = null!;
        var requested = false;
        try
        {
            await client.WaitPost(() => cfg.SetCVar(OrbitraGhostSkinCVars.SelectedSkin, OrbitraGhostSkinComponent.DefaultSkin));
            await server.WaitPost(() =>
            {
                ghost = server.EntMan.SpawnEntity("MobObserver", map.GridCoords);
                server.PlayerMan.SetAttachedEntity(player, ghost);
            });
            await pair.RunTicksSync(15);
            await client.WaitPost(() =>
            {
                gui = new GhostGui();
                client.ResolveDependency<IUserInterfaceManager>().StateRoot.AddChild(gui);
            });
            await pair.RunTicksSync(2);
            await client.WaitPost(() =>
            {
                var button = Descendants(gui).OfType<OrbitraButton>().Single(control => control.Name == "OrbitraGhostSkinsButton");
                var position = button.Size / 2;
                var pixels = position * button.UIScale;
                var screen = new ScreenCoordinates(button.GlobalPixelPosition + pixels, button.Window?.Id ?? default);
                button.KeyboardActivate(new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Down, screen, default, position, pixels));
                button.KeyboardActivate(new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Up, screen, default, position, pixels));
                window = client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<OrbitraGhostSkinWindow>().Single();
            });
            await pair.RunTicksSync(2);
            await client.WaitAssertion(() =>
            {
                Assert.That(window.IsOpen, Is.True);
                Assert.That(window.HasStyleClass("OrbitraWindowChrome"), Is.True);
                Assert.That(window.FindControl<OrbitraOptionButton>("SkinPicker").ItemCount, Is.GreaterThanOrEqualTo(2));
            });
            await client.WaitPost(() => requested = client.System<OrbitraGhostSkinSystem>().RequestSkin("OrbitraGhostSkinMauler"));
            await client.WaitAssertion(() => Assert.That(requested, Is.True));
            await pair.RunTicksSync(15);
            await client.WaitAssertion(() =>
            {
                Assert.That(cfg.GetCVar(OrbitraGhostSkinCVars.SelectedSkin), Is.EqualTo("OrbitraGhostSkinMauler"));
                Assert.That(window.FindControl<RichTextLabel>("CurrentSkin").Text,
                    Does.Contain(Loc.GetString("orbitra-ghost-skin-mauler")));
                Assert.That(window.FindControl<Button>("ApplyButton").Disabled, Is.True);
                Assert.That(window.FindControl<TextureRect>("Preview").Modulate.A, Is.EqualTo(0.45f).Within(0.001f));
            });
            await client.WaitPost(() => gui.Hide());
            await client.WaitAssertion(() => Assert.That(window.IsOpen, Is.False));
            await server.WaitPost(() =>
            {
                replacement = server.EntMan.SpawnEntity("MobObserver", map.GridCoords);
                server.PlayerMan.SetAttachedEntity(player, replacement);
            });
            await pair.RunTicksSync(20);
            await server.WaitAssertion(() => Assert.That(
                server.EntMan.GetComponent<OrbitraGhostSkinComponent>(replacement).Skin.Id, Is.EqualTo("OrbitraGhostSkinMauler")));
        }
        finally
        {
            await client.WaitPost(() =>
            {
                gui?.Dispose();
                cfg.SetCVar(OrbitraGhostSkinCVars.SelectedSkin, preference);
            });
            await server.WaitPost(() =>
            {
                server.PlayerMan.SetAttachedEntity(player, previous);
                server.EntMan.DeleteEntity(map.MapUid);
            });
            await pair.RunTicksSync(2);
        }
        await pair.CleanReturnAsync();
    }
}
