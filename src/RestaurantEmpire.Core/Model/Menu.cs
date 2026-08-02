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

        /// <summary>Dishes a kitchen carries without strain.</summary>
        public const int FreeMenuSize = 4;

        /// <summary>What each dish past that adds to ticket work and to mistakes.</summary>
        public const decimal ComplexityPerExtraDish = 0.09m;

        /// <summary>However wide the card, a kitchen does not seize up entirely.</summary>
        public const decimal MaxComplexityLoad = 1.65m;

        /// <summary>
        /// What a wide card costs the kitchen — a multiplier on ticket work and on how often a
        /// plate goes wrong. 1.0 for a tight menu, climbing from there.
        ///
        /// **Breadth used to be free, and that made "put everything on" the one dominant
        /// strategy.** Measured across six strategies and four markets: Broad Menu won every
        /// market, one distinct winner out of four. A game where a single plan wins everywhere
        /// has no decision in it, and this was why — each dish added found more guests
        /// something they wanted and nothing pushed back.
        ///
        /// A kitchen pays for breadth in prep, in mise en place, in stations juggling
        /// unrelated work and in cooks holding more in their heads. Four dishes is free;
        /// past that, each one taxes the pass.
        /// </summary>
        /// <summary>
        /// How much this card appeals to a given sort of guest, where 1.0 is a menu they have
        /// no feelings about either way. Above 1, they would go out of their way; below, they
        /// would rather eat elsewhere.
        ///
        /// **This is what makes committing to a crowd worth doing.** Menu fit used to change
        /// only what a seated guest ORDERED, never whether they came — so a fine-dining room
        /// and a pizzeria drew exactly the same people and specialising was strictly worse than
        /// hedging. Measured across six strategies and four markets, the generalist won all
        /// four. A restaurant that knows what it is should pull its own crowd in.
        /// </summary>
        public decimal AppealTo(CustomerArchetype archetype)
        {
            if (_recipeIds.Count == 0) return 1m;

            var profile = ArchetypeProfile.For(archetype);
            var total = 0m;
            var counted = 0;

            foreach (var recipe in Recipes)
            {
                var weight = 2m;   // the same neutral footing appetite starts from
                for (var i = 0; i < recipe.Tags.Count; i++) weight += profile.PullToward(recipe.Tags[i]);

                if (weight < 0.5m) weight = 0.5m;
                total += weight;
                counted++;
            }

            if (counted == 0) return 1m;

            // Normalised against that neutral 2, so a card with no opinion scores exactly 1.
            var appeal = (total / counted) / 2m;
            if (appeal < 0.35m) return 0.35m;
            return appeal > 2.2m ? 2.2m : appeal;
        }

        public decimal ComplexityLoad
        {
            get
            {
                var beyond = Count - FreeMenuSize;
                if (beyond <= 0) return 1m;

                var load = 1m + (beyond * ComplexityPerExtraDish);
                return load > MaxComplexityLoad ? MaxComplexityLoad : load;
            }
        }

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
