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
        private readonly List<string> _featured;

        internal Menu(DefinitionRegistry definitions)
        {
            _definitions = definitions;
            _recipeIds = new List<string>();
            _featured = new List<string>();
        }

        /// <summary>
        /// How many dishes can be promoted at once. Deliberately few — the design chose
        /// featured slots over a full menu-layout system precisely because scarcity is what
        /// makes promoting a dish a decision. If you could feature everything, featuring
        /// would mean nothing.
        /// </summary>
        public int FeaturedSlots { get; set; } = 2;

        /// <summary>Dishes currently promoted. Guests order these noticeably more often.</summary>
        public IReadOnlyList<string> Featured { get { return _featured; } }

        public bool IsFeatured(string recipeId)
        {
            return recipeId != null && _featured.Contains(recipeId);
        }

        /// <summary>
        /// Promotes a dish. Returns what got bumped to make room, or null if there was a
        /// free slot — because "what did this cost me?" is the whole point of the mechanic.
        /// </summary>
        public string Feature(string recipeId)
        {
            if (!Contains(recipeId))
                throw new InvalidOperationException("'" + recipeId + "' is not on the menu, so it cannot be featured.");

            if (_featured.Contains(recipeId)) return null;

            string displaced = null;
            while (_featured.Count >= FeaturedSlots && _featured.Count > 0)
            {
                displaced = _featured[0];
                _featured.RemoveAt(0);
            }

            _featured.Add(recipeId);
            return displaced;
        }

        public bool Unfeature(string recipeId)
        {
            return _featured.Remove(recipeId);
        }

        /// <summary>How much likelier a featured dish is to be ordered.</summary>
        public const int FeaturedWeight = 3;

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
            _featured.Remove(recipeId);
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
