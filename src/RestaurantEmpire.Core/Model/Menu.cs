using System;
using System.Collections.Generic;
using RestaurantEmpire.Core.Definitions;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// Which dishes a given restaurant currently sells.
    ///
    /// Holds recipe IDs, not recipe copies — so a restaurant's menu is a set of pointers
    /// into the content database, and two locations serving the same dish genuinely serve
    /// the same dish rather than two copies that can drift apart.
    /// </summary>
    public sealed class Menu
    {
        private readonly DefinitionRegistry _definitions;
        private readonly List<string> _recipeIds;

        internal Menu(DefinitionRegistry definitions)
        {
            _definitions = definitions;
            _recipeIds = new List<string>();
        }

        public IReadOnlyList<string> RecipeIds { get { return _recipeIds; } }
        public int Count { get { return _recipeIds.Count; } }

        public void Add(params string[] recipeIds)
        {
            if (recipeIds == null) return;

            foreach (var recipeId in recipeIds)
            {
                if (!_definitions.HasRecipe(recipeId))
                    throw new DefinitionNotFoundException("recipe", recipeId);

                if (!_recipeIds.Contains(recipeId))
                    _recipeIds.Add(recipeId);
            }
        }

        public bool Remove(string recipeId)
        {
            return _recipeIds.Remove(recipeId);
        }

        public bool Contains(string recipeId)
        {
            return recipeId != null && _recipeIds.Contains(recipeId);
        }

        /// <summary>The recipe definitions currently on the menu, resolved live.</summary>
        public IEnumerable<RecipeDefinition> Recipes
        {
            get
            {
                foreach (var id in _recipeIds) yield return _definitions.GetRecipe(id);
            }
        }

        /// <summary>Every distinct ingredient the current menu depends on.</summary>
        public IEnumerable<string> RequiredIngredientIds
        {
            get
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (var recipe in Recipes)
                {
                    foreach (var line in recipe.Ingredients)
                    {
                        if (seen.Add(line.IngredientId)) yield return line.IngredientId;
                    }
                }
            }
        }
    }
}
