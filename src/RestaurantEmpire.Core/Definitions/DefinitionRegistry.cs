using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Definitions
{
    /// <summary>Thrown when something references a content definition that isn't loaded.</summary>
    public sealed class DefinitionNotFoundException : Exception
    {
        public DefinitionNotFoundException(string kind, string id)
            : base("No " + kind + " definition loaded with id '" + id + "'.")
        {
            Kind = kind;
            Id = id;
        }

        public string Kind { get; }
        public string Id { get; }
    }

    /// <summary>
    /// The loaded content database: every ingredient, supplier and recipe, keyed by stable
    /// string ID (Architecture Rule 3 — never by index or load order, because indices shift
    /// when content is added or a mod is removed and IDs don't).
    ///
    /// Everything in here is immutable once loaded. Game state lives in
    /// <see cref="Model.Company"/>; this is the reference data it points at.
    /// </summary>
    public sealed class DefinitionRegistry
    {
        private readonly Dictionary<string, IngredientDefinition> _ingredients;
        private readonly Dictionary<string, SupplierDefinition> _suppliers;
        private readonly Dictionary<string, RecipeDefinition> _recipes;
        private readonly List<string> _loadWarnings;

        public DefinitionRegistry(
            IEnumerable<IngredientDefinition> ingredients,
            IEnumerable<SupplierDefinition> suppliers,
            IEnumerable<RecipeDefinition> recipes,
            IEnumerable<string> loadWarnings = null)
        {
            _ingredients = new Dictionary<string, IngredientDefinition>();
            _suppliers = new Dictionary<string, SupplierDefinition>();
            _recipes = new Dictionary<string, RecipeDefinition>();
            _loadWarnings = loadWarnings == null ? new List<string>() : new List<string>(loadWarnings);

            if (ingredients != null)
                foreach (var i in ingredients) _ingredients[i.Id] = i;

            if (suppliers != null)
                foreach (var s in suppliers) _suppliers[s.Id] = s;

            if (recipes != null)
                foreach (var r in recipes) _recipes[r.Id] = r;
        }

        public IEnumerable<IngredientDefinition> Ingredients { get { return _ingredients.Values; } }
        public IEnumerable<SupplierDefinition> Suppliers { get { return _suppliers.Values; } }
        public IEnumerable<RecipeDefinition> Recipes { get { return _recipes.Values; } }

        public int IngredientCount { get { return _ingredients.Count; } }
        public int SupplierCount { get { return _suppliers.Count; } }
        public int RecipeCount { get { return _recipes.Count; } }

        public IEnumerable<string> IngredientIds { get { return _ingredients.Keys; } }

        /// <summary>
        /// Non-fatal problems found while loading — e.g. a recipe that referenced an
        /// ingredient that no longer exists. Such content is dropped, never crashed on
        /// (Architecture Rule 3, "degrade gracefully").
        /// </summary>
        public IReadOnlyList<string> LoadWarnings { get { return _loadWarnings; } }

        public IngredientDefinition GetIngredient(string id)
        {
            IngredientDefinition found;
            if (!_ingredients.TryGetValue(id ?? string.Empty, out found))
                throw new DefinitionNotFoundException("ingredient", id);

            return found;
        }

        public SupplierDefinition GetSupplier(string id)
        {
            SupplierDefinition found;
            if (!_suppliers.TryGetValue(id ?? string.Empty, out found))
                throw new DefinitionNotFoundException("supplier", id);

            return found;
        }

        public RecipeDefinition GetRecipe(string id)
        {
            RecipeDefinition found;
            if (!_recipes.TryGetValue(id ?? string.Empty, out found))
                throw new DefinitionNotFoundException("recipe", id);

            return found;
        }

        public bool HasIngredient(string id) { return id != null && _ingredients.ContainsKey(id); }
        public bool HasSupplier(string id) { return id != null && _suppliers.ContainsKey(id); }
        public bool HasRecipe(string id) { return id != null && _recipes.ContainsKey(id); }

        /// <summary>Every recipe that uses the given ingredient — i.e. everything a supplier switch will move.</summary>
        public IEnumerable<RecipeDefinition> RecipesUsing(string ingredientId)
        {
            foreach (var recipe in _recipes.Values)
            {
                if (recipe.Uses(ingredientId)) yield return recipe;
            }
        }
    }
}
