// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Globalization;
using System.Numerics;
using Content.Client.Stylesheets.Palette;
using Content.Client._Orbitra.Stylesheets;
using Content.Client._Orbitra.UserInterface;
using Content.Shared._Orbitra.Xenobiology;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Orbitra.Xenobiology;

/// <summary>Shared presentation of specimen identity, status, portraits and bounded measurements.</summary>
internal static class OrbitraXenobiologyPresentation
{
    public static StyleBoxFlat MeterFill(Color color) => new()
    {
        BackgroundColor = color,
        BorderColor = OrbitraPalettes.PanelBorder,
        BorderThickness = new Thickness(1),
    };

    public static void StyleMeter(ProgressBar bar)
    {
        bar.BackgroundStyleBoxOverride = MeterFill(OrbitraPalettes.PanelInset);
        bar.ForegroundStyleBoxOverride = MeterFill(OrbitraPalettes.Primary.Element);
    }

    public static float Unit(float value) => float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

    public static string Number(float value) => float.IsFinite(value)
        ? value.ToString("0.#", CultureInfo.CurrentCulture) : "?";

    public static string Name(OrbitraXenobiologySpecimen specimen, string breedName, ILocalizationManager loc)
    {
        var name = string.IsNullOrWhiteSpace(specimen.Name) ? breedName : specimen.Name;
        return string.IsNullOrWhiteSpace(specimen.Identifier) || name.Contains(specimen.Identifier, StringComparison.Ordinal)
            ? name
            : loc.GetString("orbitra-xeno-identity", ("name", name), ("identifier", specimen.Identifier));
    }

    public static bool NeedsAttention(OrbitraXenobiologySpecimen specimen) => specimen.Dead || specimen.Hostile ||
        !specimen.Suspended && (specimen.Hunger < 25 || specimen.Temperature < specimen.MinimumTemperature ||
                              specimen.Temperature > specimen.MaximumTemperature);

    public static string Status(OrbitraXenobiologySpecimen specimen, ILocalizationManager loc) => loc.GetString(
        specimen.Dead ? "orbitra-xeno-dead" : specimen.Suspended ? "orbitra-xeno-contained" :
        specimen.Hostile ? "orbitra-xeno-hostile" : specimen.Temperature < specimen.MinimumTemperature ? "orbitra-xeno-cold" :
        specimen.Temperature > specimen.MaximumTemperature ? "orbitra-xeno-hot" : specimen.Hunger < 25 ? "orbitra-xeno-hungry" :
        specimen.Adult ? "orbitra-xeno-adult" : "orbitra-xeno-baby");

    public static Color StatusColor(OrbitraXenobiologySpecimen specimen) => specimen.Dead || specimen.Hostile
        ? Palettes.Red.Text : NeedsAttention(specimen) ? Palettes.Amber.Text : Palettes.Green.Text;

    public static void SetIcon(TextureRect icon, SpriteSystem sprite, OrbitraXenobiologyBreedPrototype breed,
        bool adult = false, bool dead = false)
    {
        var path = adult ? "/Textures/_Orbitra/Xenobiology/Mobs/slimesAdult.rsi" : "/Textures/_Orbitra/Xenobiology/Mobs/slimesBaby.rsi";
        icon.Texture = sprite.Frame0(new SpriteSpecifier.Rsi(new ResPath(path), dead ? "dead" : "base"));
        icon.Modulate = breed.Color;
    }

    public static void SetPortrait(EntityPrototypeView portrait, IEntityManager entity,
        OrbitraXenobiologyBreedPrototype breed, OrbitraXenobiologySpecimen specimen)
    {
        portrait.SetPrototype(breed.Mob);
        if (portrait.Entity is not { } preview)
            return;
        var stage = specimen.Adult
            ? specimen.Dead ? OrbitraXenobiologyStage.DeadAdult : OrbitraXenobiologyStage.Adult
            : specimen.Dead ? OrbitraXenobiologyStage.DeadBaby : OrbitraXenobiologyStage.Baby;
        entity.System<SharedAppearanceSystem>().SetData(preview.Owner, OrbitraXenobiologyVisuals.Stage, stage);
        entity.System<SpriteSystem>().ForceUpdate(preview.Owner);
    }

    public static Vector2 Available(Vector2 viewport) => Vector2.Max(Vector2.One, viewport - new Vector2(OrbitraUiMetrics.ScreenMargin * 2));
}
