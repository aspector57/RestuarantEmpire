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
        private readonly Dictionary<string, EquipmentDefinition> _equipment;
        private readonly Dictionary<string, ConceptDefinition> _concepts;
        private readonly Dictionary<string, CountryDefinition> _countries;
        private readonly List<string> _loadWarnings;

        public DefinitionRegistry(
            IEnumerable<IngredientDefinition> ingredients,
            IEnumerable<SupplierDefinition> suppliers,
            IEnumerable<RecipeDefinition> recipes,
            IEnumerable<string> loadWarnings = null,
            IEnumerable<EquipmentDefinition> equipment = null,
            IEnumerable<ConceptDefinition> concepts = null,
            IEnumerable<CountryDefinition> countries = null,
            IEnumerable<DishExtraDefinition> extras = null,
            IReadOnlyDictionary<string, decimal> liftCeilings = null)
        {
            _ingredients = new Dictionary<string, IngredientDefinition>();
            _suppliers = new Dictionary<string, SupplierDefinition>();
            _recipes = new Dictionary<string, RecipeDefinition>();
            _equipment = new Dictionary<string, EquipmentDefinition>();
            _concepts = new Dictionary<string, ConceptDefinition>();
            _countries = new Dictionary<string, CountryDefinition>();
            _extras = new Dictionary<string, List<DishExtraDefinition>>();
            _liftCeilings = new Dictionary<string, decimal>();
            _loadWarnings = loadWarnings == null ? new List<string>() : new List<string>(loadWarnings);

            if (ingredients != null)
                foreach (var i in ingredients) _ingredients[i.Id] = i;

            if (suppliers != null)
                foreach (var s in suppliers) _suppliers[s.Id] = s;

            if (recipes != null)
                foreach (var r in recipes) _recipes[r.Id] = r;

            if (equipment != null)
                foreach (var e in equipment) _equipment[e.Id] = e;

            if (concepts != null)
                foreach (var c in concepts) _concepts[c.Id] = c;

            if (countries != null)
                foreach (var c in countries) _countries[c.Id] = c;

            if (extras != null)
                foreach (var e in extras)
                {
                    List<DishExtraDefinition> forDish;
                    if (!_extras.TryGetValue(e.RecipeId, out forDish))
                    {
                        forDish = new List<DishExtraDefinition>();
                        _extras[e.RecipeId] = forDish;
                    }
                    forDish.Add(e);
                }

            if (liftCeilings != null)
                foreach (var kv in liftCeilings) _liftCeilings[kv.Key] = kv.Value;
        }

        private readonly Dictionary<string, List<DishExtraDefinition>> _extras;
        private readonly Dictionary<string, decimal> _liftCeilings;

        /// <summary>What may be added to this dish. Empty for a dish nothing has been written for.</summary>
        public IReadOnlyList<DishExtraDefinition> ExtrasFor(string recipeId)
        {
            List<DishExtraDefinition> forDish;
            if (recipeId != null && _extras.TryGetValue(recipeId, out forDish)) return forDish;
            return new List<DishExtraDefinition>();
        }

        public DishExtraDefinition GetExtra(string recipeId, string extraId)
        {
            foreach (var e in ExtrasFor(recipeId)) if (e.Id == extraId) return e;
            return null;
        }

        /// <summary>
        /// How far a dish of this kind can be dressed up before the plate stops carrying it.
        /// A focaccia cannot be lifted into a main course however much is put on it.
        /// </summary>
        public decimal LiftCeilingFor(string category)
        {
            decimal ceiling;
            if (category != null && _liftCeilings.TryGetValue(category, out ceiling)) return ceiling;
            return 0.40m;
        }

        /// <summary>Markets you can trade in. A country is a Region with a market attached.</summary>
        public IEnumerable<CountryDefinition> Countries { get { return _countries.Values; } }

        public int CountryCount { get { return _countries.Count; } }

        public bool HasCountry(string id) { return id != null && _countries.ContainsKey(id); }

        public CountryDefinition GetCountry(string id)
        {
            CountryDefinition found;
            if (id == null || !_countries.TryGetValue(id, out found))
                throw new DefinitionNotFoundException("country", id);

            return found;
        }

        /// <summary>
        /// Restaurant concepts the player can start from — a card, a price position and the
        /// hours. Data rather than code, so a new one is a JSON file. See ConceptDefinition
        /// for why these stop short of prescribing a build.
        /// </summary>
        public IEnumerable<ConceptDefinition> Concepts { get { return _concepts.Values; } }

        public int ConceptCount { get { return _concepts.Count; } }

        public bool HasConcept(string id) { return id != null && _concepts.ContainsKey(id); }

        public ConceptDefinition GetConcept(string id)
        {
            ConceptDefinition found;
            if (id == null || !_concepts.TryGetValue(id, out found))
                throw new DefinitionNotFoundException("concept", id);

            return found;
        }

        public IEnumerable<IngredientDefinition> Ingredients { get { return _ingredients.Values; } }
        public IEnumerable<SupplierDefinition> Suppliers { get { return _suppliers.Values; } }
        public IEnumerable<RecipeDefinition> Recipes { get { return _recipes.Values; } }

        public int IngredientCount { get { return _ingredients.Count; } }
        public int SupplierCount { get { return _suppliers.Count; } }
        public int RecipeCount { get { return _recipes.Count; } }
        public int EquipmentCount { get { return _equipment.Count; } }

        /// <summary>The whole catalogue you can buy a kitchen from.</summary>
        public IEnumerable<EquipmentDefinition> Equipment { get { return _equipment.Values; } }

        /// <summary>What you can buy for one station, cheapest first.</summary>
        public IEnumerable<EquipmentDefinition> EquipmentFor(string stationId)
        {
            var matches = new List<EquipmentDefinition>();
            foreach (var item in _equipment.Values)
            {
                if (item.StationId == stationId) matches.Add(item);
            }

            matches.Sort((a, b) => a.Cost.CompareTo(b.Cost));
            return matches;
        }

        public bool HasEquipment(string id) { return id != null && _equipment.ContainsKey(id); }

        public EquipmentDefinition GetEquipment(string id)
        {
            EquipmentDefinition found;
            if (!_equipment.TryGetValue(id ?? string.Empty, out found))
                throw new DefinitionNotFoundException("equipment", id);

            return found;
        }

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
