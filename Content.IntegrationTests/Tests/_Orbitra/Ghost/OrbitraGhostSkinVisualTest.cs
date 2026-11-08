// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Linq;
using Content.Shared._Orbitra.Ghost;
using Content.Shared.Ghost.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra.Ghost;

[TestFixture, NonParallelizable]
public sealed class OrbitraGhostSkinVisualTest
{
    [TestCase("OrbitraGhostSkinMauler")]
    [TestCase("OrbitraGhostSkinHoloCarp")]
    [TestCase("OrbitraGhostSkinDragon")]
    public async Task ReplacementPreservesNativeDeathStateAndRestoresTint(string selectedSkin)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var uid = entities.SpawnEntity("MobObserver", MapCoordinates.Nullspace);
            try
            {
                var sprite = entities.GetComponent<SpriteComponent>(uid);
                var sprites = entities.System<SpriteSystem>();
                var appearance = entities.System<AppearanceSystem>();
                var skin = entities.EnsureComponent<OrbitraGhostSkinComponent>(uid);
                var originalColor = sprite.Color;
                var originalCount = sprite.AllLayers.Count();
                Assert.That(sprites.TryGetLayer((uid, sprite), "ghostVariant", out var native, false), Is.True);
                var originalVisible = native!.Visible;
                appearance.SetData(uid, GhostVisuals.Damage, "ghost_Cold0");
                appearance.OnChangeData(uid, sprite);
                Assert.That(native.State.Name, Is.EqualTo("ghost_Cold0"));

                sprites.SetVisible((uid, sprite), false);
                skin.Skin = selectedSkin;
                appearance.OnChangeData(uid, sprite);
                var replacement = sprite.AllLayers.Last();
                Assert.Multiple(() =>
                {
                    Assert.That(sprite.Color, Is.EqualTo(Color.White));
                    Assert.That(replacement.Color.A, Is.EqualTo(0.45f).Within(0.001f));
                    Assert.That(replacement.Visible, Is.True);
                    Assert.That(native.Visible, Is.False);
                    Assert.That(sprite.Visible, Is.False);
                    Assert.That(sprite.AllLayers.Count(), Is.EqualTo(originalCount + 1));
                });

                // Штатный визуализатор продолжает обновлять скрытый слой причины смерти.
                appearance.SetData(uid, GhostVisuals.Damage, "ghost_Heat0");
                appearance.OnChangeData(uid, sprite);
                Assert.That(native.State.Name, Is.EqualTo("ghost_Heat0"));
                Assert.That(native.Visible, Is.False);

                for (var i = 0; i < 3; i++)
                {
                    skin.Skin = OrbitraGhostSkinComponent.DefaultSkin;
                    appearance.OnChangeData(uid, sprite);
                    Assert.Multiple(() =>
                    {
                        Assert.That(sprite.Color, Is.EqualTo(originalColor));
                        Assert.That(native.Visible, Is.EqualTo(originalVisible));
                        Assert.That(native.State.Name, Is.EqualTo("ghost_Heat0"));
                        Assert.That(replacement.Visible, Is.False);
                        Assert.That(sprite.Visible, Is.False);
                    });
                    skin.Skin = selectedSkin;
                    appearance.OnChangeData(uid, sprite);
                    Assert.That(sprite.AllLayers.Count(), Is.EqualTo(originalCount + 1));
                }

                entities.RemoveComponent<OrbitraGhostSkinComponent>(uid);
                Assert.That(sprite.Color, Is.EqualTo(originalColor));
                Assert.That(native.Visible, Is.EqualTo(originalVisible));
            }
            finally
            {
                entities.DeleteEntity(uid);
            }
        });
        await pair.CleanReturnAsync();
    }
}
