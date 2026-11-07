// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Linq;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Research.UI;

/// <summary>
/// Cached, localized presentation data shared by the graph and the details pane.
/// </summary>
internal sealed class OrbitraResearchTreeCatalog
{
    public readonly Dictionary<ProtoId<TechnologyPrototype>, OrbitraResearchTechnology> Technologies = new();
    public readonly Dictionary<ProtoId<TechDisciplinePrototype>, TechDisciplinePrototype> Disciplines = new();
    public readonly Dictionary<ProtoId<TechDisciplinePrototype>, Texture> DisciplineIcons = new();

    public OrbitraResearchTreeCatalog(
        IPrototypeManager prototype,
        ILocalizationManager loc,
        SpriteSystem sprite,
        SharedLatheSystem lathe,
        IEnumerable<ProtoId<TechDisciplinePrototype>> supported)
    {
        foreach (var id in supported)
        {
            if (!prototype.TryIndex(id, out var discipline))
                continue;

            Disciplines.Add(id, discipline);
            DisciplineIcons.Add(id, sprite.Frame0(discipline.Icon));
        }

        var recipes = new Dictionary<ProtoId<LatheRecipePrototype>, OrbitraResearchRecipe>();
        foreach (var technology in prototype.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (technology.Hidden || !Disciplines.ContainsKey(technology.Discipline))
                continue;

            var unlocks = new List<OrbitraResearchRecipe>();
            foreach (var recipeId in technology.RecipeUnlocks)
            {
                if (!prototype.TryIndex(recipeId, out var recipe))
                    continue;

                if (!recipes.TryGetValue(recipeId, out var presentation))
                {
                    var name = lathe.GetRecipeName(recipe);
                    if (string.IsNullOrWhiteSpace(name))
                        name = loc.GetString("orbitra-research-tree-unnamed-recipe");

                    // Orbitra-Edit: пустой ResultReagents не поддерживает First() в описании станка.
                    var description = recipe.Description != null || recipe.Result != null || recipe.ResultReagents?.Count > 0
                        ? lathe.GetRecipeDescription(recipe)
                        : string.Empty;
                    Texture? icon = null;
                    if (recipe.Icon != null)
                        icon = sprite.Frame0(recipe.Icon);
                    else if (recipe.Result is { } result && prototype.TryIndex(result, out var entity))
                        icon = sprite.Frame0(entity);

                    presentation = new OrbitraResearchRecipe(name, description, icon);
                    recipes.Add(recipeId, presentation);
                }

                unlocks.Add(presentation);
            }

            var title = loc.GetString(technology.Name);
            var key = GetDescriptionKey(technology);
            var purpose = loc.TryGetString(key, out var descriptionText)
                ? descriptionText
                : loc.GetString("orbitra-research-tree-description-fallback");
            Technologies.Add(technology.ID, new OrbitraResearchTechnology(
                technology,
                title,
                purpose,
                loc.GetString("orbitra-research-tree-node-meta", ("tier", technology.Tier), ("cost", technology.Cost)),
                sprite.Frame0(technology.Icon),
                unlocks,
                string.Join('\n', unlocks.Select(recipe => recipe.Name).Prepend(title))));
        }
    }

    /// <summary>
    /// Normalizes legacy technology names for the fork's description keys.
    /// </summary>
    public static string GetDescriptionKey(TechnologyPrototype technology)
    {
        return $"orbitra-{technology.Name.ToString().Replace('_', '-').ToLowerInvariant()}-description";
    }
}

internal sealed record OrbitraResearchRecipe(string Name, string Description, Texture? Icon);

internal sealed record OrbitraResearchTechnology(
    TechnologyPrototype Prototype,
    string Name,
    string Description,
    string Metadata,
    Texture Icon,
    IReadOnlyList<OrbitraResearchRecipe> Recipes,
    string SearchText);

internal enum OrbitraResearchNodeState : byte
{
    Locked,
    Available,
    Researched,
}
