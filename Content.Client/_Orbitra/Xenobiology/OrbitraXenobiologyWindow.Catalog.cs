// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Linq;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Client._Orbitra.UserInterface;
using Content.Shared._Orbitra.Xenobiology;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Orbitra.Xenobiology;

public sealed partial class OrbitraXenobiologyWindow
{
    /// <summary>Catalog navigation and recipes are independent of the live specimen refresh.</summary>
    private readonly Dictionary<string, List<OrbitraXenobiologyReactionPrototype>> _reactions = new();

    private void BuildCatalog()
    {
        foreach (var reaction in _prototype.EnumeratePrototypes<OrbitraXenobiologyReactionPrototype>())
        {
            if (!_reactions.TryGetValue(reaction.Breed.Id, out var recipes))
                _reactions.Add(reaction.Breed.Id, recipes = new List<OrbitraXenobiologyReactionPrototype>());
            recipes.Add(reaction);
        }
        foreach (var breed in _breeds)
        {
            var button = new OrbitraButton
            {
                Text = _loc.GetString("orbitra-xeno-breed-button", ("tier", breed.Tier), ("name", _loc.GetString(breed.Name))),
                HorizontalExpand = true,
                ClipText = true,
                ToggleMode = true,
                ToolTip = _loc.GetString(breed.Description),
            };
            button.OnPressed += _ => SelectBreed(breed.ID);
            BreedList.AddChild(button);
            _breedButtons.Add(breed.ID, button);
        }
    }

    private void ApplyCatalogFilter()
    {
        var search = CatalogSearch.Text.Trim();
        var visible = 0;
        foreach (var breed in _breeds)
        {
            var button = _breedButtons[breed.ID];
            button.Visible = (_catalogTier < 0 || breed.Tier == _catalogTier) &&
                (search.Length == 0 || _loc.GetString(breed.Name).Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                 _loc.GetString(breed.Description).Contains(search, StringComparison.CurrentCultureIgnoreCase));
            if (button.Visible)
                visible++;
        }
        CatalogEmpty.Visible = visible == 0;
        UpdateSummary();
    }

    /// <summary>Opens a lineage, clearing a search that could otherwise conceal a mutation target.</summary>
    public bool SelectBreed(string id) => SelectBreed(id, true);

    private bool SelectBreed(string id, bool showDetails)
    {
        if (!_prototype.TryIndex<OrbitraXenobiologyBreedPrototype>(id, out var breed) || !_breedButtons.ContainsKey(id))
            return false;
        if (showDetails)
        {
            if (!_breedButtons[id].Visible)
            {
                _catalogTier = -1;
                CatalogTierFilter.SelectId(-1);
                CatalogSearch.SetText(string.Empty);
                ApplyCatalogFilter();
            }
            _showCatalogDetails = true;
        }
        foreach (var (breedId, button) in _breedButtons)
            button.Pressed = breedId == id;
        UpdateAdaptiveLayout();
        if (_selectedBreed == id)
            return true;
        _selectedBreed = id;
        CatalogDetailsScroll.SetScrollValue(Vector2.Zero);
        CatalogRecipesScroll.SetScrollValue(Vector2.Zero);
        BreedDetails.RemoveAllChildren();
        BreedRecipes.RemoveAllChildren();
        OrbitraXenobiologyPresentation.SetIcon(CatalogPortrait, _sprite, breed);
        CatalogName.Text = _loc.GetString(breed.Name);
        CatalogName.ToolTip = CatalogName.Text;
        CatalogTier.Text = _loc.GetString("orbitra-xeno-tier", ("tier", breed.Tier));
        BreedDetails.AddChild(CatalogText(_loc.GetString(breed.Description)));
        BreedDetails.AddChild(new Label { Text = _loc.GetString("orbitra-xeno-care"), StyleClasses = { StyleClass.LabelHeading } });
        AddCatalogStat("orbitra-xeno-care-duration", _loc.GetString("orbitra-xeno-seconds", ("seconds", breed.GrowthDuration)));
        AddCatalogStat("orbitra-xeno-care-growth-biomass", OrbitraXenobiologyPresentation.Number(breed.GrowthBiomass));
        AddCatalogStat("orbitra-xeno-care-division-biomass", OrbitraXenobiologyPresentation.Number(breed.DivisionBiomass));
        AddCatalogStat("orbitra-xeno-care-temperature", _loc.GetString("orbitra-xeno-care-temperature-value",
            ("minimum", OrbitraXenobiologyPresentation.Number(breed.MinimumTemperature - 273.15f)),
            ("maximum", OrbitraXenobiologyPresentation.Number(breed.MaximumTemperature - 273.15f))));
        BreedDetails.AddChild(CatalogText(_loc.GetString("orbitra-xeno-care-thresholds")));
        BreedDetails.AddChild(new Label { Text = _loc.GetString("orbitra-xeno-mutation-heading"), StyleClasses = { StyleClass.LabelHeading } });
        if (breed.Mutations.Count == 0)
            BreedDetails.AddChild(CatalogText(_loc.GetString("orbitra-xeno-stable-lineage")));
        else
        {
            var total = breed.Mutations.Values.Sum();
            BreedDetails.AddChild(CatalogText(_loc.GetString("orbitra-xeno-mutation-explanation")));
            foreach (var (target, weight) in breed.Mutations.OrderBy(pair => pair.Key.Id))
            {
                var mutation = _prototype.Index(target);
                var text = _loc.GetString("orbitra-xeno-mutation-target", ("name", _loc.GetString(mutation.Name)),
                    ("chance", OrbitraXenobiologyPresentation.Number(total > 0 ? weight / total * 100 : 0)));
                var button = new OrbitraButton
                {
                    Text = text,
                    ToolTip = text,
                    HorizontalExpand = true,
                    ClipText = true,
                };
                button.OnPressed += _ => SelectBreed(target.Id);
                BreedDetails.AddChild(button);
            }
        }
        BreedRecipes.AddChild(CatalogText(_loc.GetString("orbitra-xeno-extract-instruction")));
        if (_reactions.TryGetValue(id, out var recipes))
        {
            foreach (var reaction in recipes.OrderBy(recipe => recipe.ID))
                AddRecipe(reaction);
        }
        foreach (var industrial in _prototype.EnumeratePrototypes<OrbitraXenobiologyIndustrialRecipePrototype>().OrderBy(recipe => recipe.ID))
        {
            if (industrial.Breed.Id != id)
                continue;

            var panel = new PanelContainer { StyleClasses = { "OrbitraWindowSurface" } };
            var recipe = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6, Margin = new Thickness(8) };
            recipe.AddChild(new Label { Text = _loc.GetString("orbitra-xeno-industrial-heading"), ClipText = true, StyleClasses = { StyleClass.LabelHeading } });
            recipe.AddChild(CatalogText(_loc.GetString("orbitra-xeno-industrial-catalog", ("required", industrial.RequiredExtracts))));
            recipe.AddChild(CatalogText(_loc.GetString("orbitra-xeno-industrial-cycle", ("plasma", industrial.PlasmaCost),
                ("seconds", industrial.Interval.TotalSeconds))));
            foreach (var (product, count) in industrial.Products)
                recipe.AddChild(CatalogText(_loc.GetString("orbitra-xeno-industrial-product", ("count", count), ("name", _prototype.Index(product).Name))));
            panel.AddChild(recipe);
            BreedRecipes.AddChild(panel);
        }
        return true;
    }

    private void AddRecipe(OrbitraXenobiologyReactionPrototype reaction)
    {
        var panel = new PanelContainer { StyleClasses = { "OrbitraWindowSurface" } };
        var recipe = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6, Margin = new Thickness(8) };
        var heading = _loc.GetString(reaction.Name);
        recipe.AddChild(new Label { Text = heading, ToolTip = heading, ClipText = true, StyleClasses = { StyleClass.LabelHeading } });
        recipe.AddChild(CatalogText(_loc.GetString("orbitra-xeno-catalyst", ("amount", reaction.Amount),
            ("reagent", _prototype.Index(reaction.Reagent).LocalizedName))));
        recipe.AddChild(CatalogText(_loc.GetString(reaction.Description)));
        foreach (var (product, count) in reaction.Products)
        {
            var output = new BoxContainer { SeparationOverride = 8 };
            output.AddChild(new TextureRect
            {
                Texture = _sprite.GetPrototypeIcon(product).Default,
                SetSize = new Vector2(32),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
            });
            var productText = CatalogText(_loc.GetString("orbitra-xeno-product", ("count", count), ("name", _prototype.Index(product).Name)));
            productText.VerticalAlignment = VAlignment.Center;
            output.AddChild(productText);
            recipe.AddChild(output);
        }
        foreach (var (reagent, amount) in reaction.Reagents)
            recipe.AddChild(CatalogText(_loc.GetString("orbitra-xeno-reagent-product", ("amount", amount),
                ("name", _prototype.Index(reagent).LocalizedName))));
        panel.AddChild(recipe);
        BreedRecipes.AddChild(panel);
    }

    private void ShowCatalogRecipes(bool recipes)
    {
        CatalogCareButton.Pressed = !recipes;
        CatalogRecipesButton.Pressed = recipes;
        CatalogDetailsScroll.Visible = !recipes;
        CatalogRecipesScroll.Visible = recipes;
    }

    private void AddCatalogStat(string key, string amount)
    {
        var caption = _loc.GetString(key);
        var row = new BoxContainer { SeparationOverride = 8, HorizontalExpand = true };
        row.AddChild(new Label { Text = caption, ToolTip = caption, ClipText = true, HorizontalExpand = true, StyleClasses = { StyleClass.LabelSubText } });
        row.AddChild(new Label { Text = amount });
        BreedDetails.AddChild(row);
    }

    private static RichTextLabel CatalogText(string text)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromUnformatted(text));
        return label;
    }
}
