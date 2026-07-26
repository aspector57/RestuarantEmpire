using System;
using RestaurantEmpire.Core.Definitions;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// Computes what a dish costs and what it earns, live, from the supplier assignments
    /// in force at the moment of the call.
    ///
    /// This class holds NO cached values by design. Every method walks the recipe's
    /// ingredient lines, asks the current <see cref="SupplierPolicy"/> what each one costs
    /// right now, and adds it up. That is why switching a supplier needs zero manual edits:
    /// there was never a stored number to go stale in the first place.
    /// </summary>
    public sealed class MenuCosting
    {
        private readonly DefinitionRegistry _definitions;
        private readonly SupplierPolicy _policy;
        private readonly PricingPolicy _pricing;

        public MenuCosting(DefinitionRegistry definitions, SupplierPolicy policy, PricingPolicy pricing)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (pricing == null) throw new ArgumentNullException(nameof(pricing));

            _definitions = definitions;
            _policy = policy;
            _pricing = pricing;
        }

        /// <summary>
        /// What this restaurant charges for the dish right now — the player's price if one
        /// is set anywhere up the chain, otherwise the price the recipe shipped with.
        /// Every figure below is computed against this, never against the raw definition.
        /// </summary>
        public decimal MenuPrice(string recipeId)
        {
            return _pricing.ResolvePrice(recipeId);
        }

        /// <summary>
        /// Total ingredient cost to put one plate of this dish in front of a guest,
        /// at today's supplier assignments.
        /// </summary>
        public decimal PlateCost(string recipeId)
        {
            var recipe = _definitions.GetRecipe(recipeId);
            var total = 0m;

            for (var i = 0; i < recipe.Ingredients.Count; i++)
            {
                var line = recipe.Ingredients[i];
                total += _policy.UnitPriceFor(line.IngredientId) * line.Quantity;
            }

            return total;
        }

        /// <summary>
        /// Menu price minus plate cost — the money the dish actually contributes.
        /// One of the two axes of the Kasavana-Smith matrix.
        /// </summary>
        public decimal ContributionMargin(string recipeId)
        {
            return MenuPrice(recipeId) - PlateCost(recipeId);
        }

        /// <summary>Contribution margin as a share of menu price (0.72 = 72% of the price is margin).</summary>
        public decimal ContributionMarginRatio(string recipeId)
        {
            var price = MenuPrice(recipeId);
            return price == 0m ? 0m : ContributionMargin(recipeId) / price;
        }

        /// <summary>
        /// Plate cost as a share of menu price. The industry benchmark is roughly 28-35%
        /// (design doc Phase 2); this is the per-dish half of what later rolls up into prime cost.
        /// </summary>
        public decimal FoodCostRatio(string recipeId)
        {
            var price = MenuPrice(recipeId);
            return price == 0m ? 0m : PlateCost(recipeId) / price;
        }

        /// <summary>
        /// How good this dish's ingredients currently are, 0 to 1, from the quality tiers of
        /// whichever suppliers are assigned right now.
        ///
        /// Live like everything else here, which is the interesting part: switching to a
        /// cheaper supplier raises margin and lowers this in the same instant, and guests
        /// taste the difference. That is the tradeoff the whole sourcing system exists to create.
        /// </summary>
        public decimal IngredientQuality(string recipeId)
        {
            var recipe = _definitions.GetRecipe(recipeId);
            if (recipe.Ingredients.Count == 0) return 0m;

            var totalTier = 0m;
            for (var i = 0; i < recipe.Ingredients.Count; i++)
                totalTier += _policy.ResolveSupplier(recipe.Ingredients[i].IngredientId).QualityTier;

            // Tiers run 1..5, so this lands in 0.2 (budget) .. 1.0 (premium).
            return (totalTier / recipe.Ingredients.Count) / 5m;
        }
    }
}
