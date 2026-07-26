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

        public MenuCosting(DefinitionRegistry definitions, SupplierPolicy policy)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            _definitions = definitions;
            _policy = policy;
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
            return _definitions.GetRecipe(recipeId).MenuPrice - PlateCost(recipeId);
        }

        /// <summary>Contribution margin as a share of menu price (0.72 = 72% of the price is margin).</summary>
        public decimal ContributionMarginRatio(string recipeId)
        {
            var price = _definitions.GetRecipe(recipeId).MenuPrice;
            return price == 0m ? 0m : ContributionMargin(recipeId) / price;
        }

        /// <summary>
        /// Plate cost as a share of menu price. The industry benchmark is roughly 28-35%
        /// (design doc Phase 2); this is the per-dish half of what later rolls up into prime cost.
        /// </summary>
        public decimal FoodCostRatio(string recipeId)
        {
            var price = _definitions.GetRecipe(recipeId).MenuPrice;
            return price == 0m ? 0m : PlateCost(recipeId) / price;
        }
    }
}
